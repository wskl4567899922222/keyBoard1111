#!/usr/bin/env node
/**
 * 参考实现：单词音频 URL / 拆分时间段选择逻辑
 * 规则来源：飞书文档《解密后的单词 JSON 音频与拆分播放说明》
 *
 * 核心规则：
 *   美音只看 v_status.us，英音只看 v_status.uk（1=真人，0=非真人）。
 *   音频前缀：https://static.jtsee.com/app_media/words_mp3_v2
 *   完整音频（整词播放）：                拆分音频（按时间段播放）：
 *     us=1: /us/alysa_word/{word}          /us/alysa_split/{split}
 *     us=0: /us/aria_word/{old_word}       /us/aria_split/{old_split}
 *     uk=1: /gb/scott_word/{word}          /gb/scott_split/{split}
 *     uk=0: /gb/ryan_word/{old_word}       /gb/ryan_split/{old_split}
 *   时间段字段与拆分音频目录的配对（字段名里的 aria/ryan 不代表目录）：
 *     us=1 -> mySpeech_us_aria_split + us/alysa_split
 *     us=0 -> oldSpeech_us_aria_split + us/aria_split
 *     uk=1 -> mySpeech_en_ryan_split  + gb/scott_split
 *     uk=0 -> oldSpeech_en_ryan_split + gb/ryan_split
 *   时间数组结构：[整读, 片段1, 片段2, ..., 整读]；点击第 n 个片段取数组第 n+1 项。
 *   播放区间 [s, s+d]，毫秒，保留小数，按各自的 s/d 独立播放。
 *
 * 用法：
 *   node word-audio.js <解密后的单词JSON> <us|en> [real|old]
 *     第三个参数可选：强制指定真人/非真人（默认按 v_status）。
 *   例：node word-audio.js word_3357.decrypted.json us
 */
const fs = require('node:fs');

const AUDIO_BASE = 'https://static.jtsee.com/app_media/words_mp3_v2';

/** 解析内嵌 JSON 字符串字段；失败返回 null。 */
function parseJsonField(value) {
  if (value == null) return null;
  if (typeof value === 'object') return value; // 有些数据已是对象
  try { return JSON.parse(value); } catch { return null; }
}

/**
 * 从解密后的单词 JSON 计算某口音的音频方案。
 * @param {object} wordJson 顶层 { wid, wordInfo, spoken, syllables }
 * @param {'us'|'en'} accent 口音
 * @param {'real'|'old'|null} force 强制真人/非真人（null = 按 v_status）
 * @returns {{ ok:boolean, reason?:string, ...方案 }}
 */
function resolveAudio(wordJson, accent, force = null) {
  const wi = wordJson.wordInfo || {};
  const sy = wordJson.syllables || {};
  const status = parseJsonField(wi.v_status);
  const files = parseJsonField(wi.audio_file_name);

  if (!status || (status.us == null && status.uk == null)) {
    return { ok: false, reason: 'v_status 缺失或解析失败' };
  }
  if (!files || !files.word || !files.split || !files.old_word || !files.old_split) {
    return { ok: false, reason: 'audio_file_name 缺失或字段不全' };
  }

  const isReal = force ? force === 'real' : Number(accent === 'us' ? status.us : status.uk) === 1;

  // 音频文件名与目录、时间段字段配对表
  const table = {
    us: { real: { word: 'us/alysa_word', split: 'us/alysa_split', time: 'mySpeech_us_aria_split' },
          old:  { word: 'us/aria_word',  split: 'us/aria_split',  time: 'oldSpeech_us_aria_split' } },
    en: { real: { word: 'gb/scott_word', split: 'gb/scott_split', time: 'mySpeech_en_ryan_split' },
          old:  { word: 'gb/ryan_word',  split: 'gb/ryan_split',  time: 'oldSpeech_en_ryan_split' } },
  };
  const cfg = table[accent][isReal ? 'real' : 'old'];

  const fullUrl = `${AUDIO_BASE}/${cfg.word}/${isReal ? files.word : files.old_word}`;
  const splitUrl = `${AUDIO_BASE}/${cfg.split}/${isReal ? files.split : files.old_split}`;
  const times = parseJsonField(sy[cfg.time]);
  if (!Array.isArray(times) || times.length === 0) {
    return { ok: false, reason: `时间段字段 ${cfg.time} 缺失或解析失败` };
  }

  const mySyll = parseJsonField(wi.my_syllable || sy.my_syllable);
  const pieces = mySyll ? String(mySyll.ws).split('-') : null;

  return {
    ok: true,
    accent, isReal,
    fullUrl,           // 整词播放用
    splitUrl,          // 分段播放用
    timeField: cfg.time,
    times,             // [整读, 片段1..., 整读]
    pieces,            // my_syllable.ws 按连字符拆分的可点击片段
    /**
     * 点击第 index 个片段（0 起）对应的播放区间。
     * 按位置对应，不按 t 文本匹配；数组结构 [整读, 片段..., 整读]。
     */
    segment(index) {
      const item = times[index + 1]; // 第 n 个片段 = 数组第 n+1 项
      if (!item || typeof item.s !== 'number' || typeof item.d !== 'number') return null;
      return { startMs: item.s, endMs: item.s + item.d, label: item.t };
    },
    /** 校验可点击片段数与时间数组中间项数是否一致。 */
    valid() {
      return Array.isArray(pieces) && pieces.length === times.length - 2;
    },
  };
}

module.exports = { resolveAudio, AUDIO_BASE, parseJsonField };

// ---------- CLI ----------
if (require.main === module) {
  const [file, accent, force] = process.argv.slice(2);
  if (!file || !accent) {
    console.log('用法：node word-audio.js <解密后的单词JSON> <us|en> [real|old]');
    process.exit(0);
  }
  const plan = resolveAudio(JSON.parse(fs.readFileSync(file, 'utf8')), accent, force || null);
  if (!plan.ok) {
    console.error(`❌ ${plan.reason}`);
    process.exitCode = 1;
  } else {
    console.log(`口音 ${plan.accent}｜${plan.isReal ? '真人' : '非真人'}｜时间段字段 ${plan.timeField}`);
    console.log(`完整音频: ${plan.fullUrl}`);
    console.log(`拆分音频: ${plan.splitUrl}`);
    console.log(`可点击片段: ${(plan.pieces || []).join(' / ')}（片段数与时间段${plan.valid() ? '匹配 ✅' : '不匹配 ❌'}）`);
    console.log('时间段数组:');
    plan.times.forEach((it, i) => {
      const kind = i === 0 || i === plan.times.length - 1 ? '整读' : `片段${i}`;
      console.log(`  [${i}] ${kind}  s=${it.s} d=${it.d} t=${it.t}`);
    });
  }
}
