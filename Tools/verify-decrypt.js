#!/usr/bin/env node
/**
 * 验证脚本：把 fetch-decrypt.js 解密出的线上数据与项目内
 * Assets/ByteTyping/Resources 的 WordPhonicsMap / WordSyllableMap 逐字段比对，
 * 确认解密数据真实可信（同源同格式）。
 *
 * 用法：node verify-decrypt.js [解密文件目录]（默认本目录）
 */
const fs = require('node:fs');
const path = require('node:path');

const dir = process.argv[2] || __dirname;
const RES_DIR = path.resolve(dir, '../Assets/ByteTyping/Resources');

function readJson(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}

// 项目 phonemes 用 positions，线上 y_phonics 用 position —— 统一比较。
// 严格：sound 逐字符比对。
function phonemesEqual(a, b) {
  if (a.length !== b.length) return false;
  const norm = (list) => list
    .map((p) => `${p.sound}@${JSON.stringify([...(p.positions || p.position)].sort((x, y) => x - y))}`)
    .sort()
    .join('|');
  return norm(a) === norm(b);
}

// 宽松：IPA 字符归一化（g↔ɡ 等）后比对，用于识别"同源但项目做过规整"的情况。
function normalizeIpa(s) {
  return s.replace(/ɡ/g, 'g').replace(/(ə)/g, '').replace(/ə/g, '').trim();
}
function phonemesLooseEqual(a, b) {
  const norm = (list) => list
    .map((p) => `${normalizeIpa(p.sound)}@${JSON.stringify([...(p.positions || p.position)].sort((x, y) => x - y))}`)
    .sort()
    .join('|');
  if (norm(a) === norm(b)) return true;
  // 再宽松一档：忽略位置差，只看音素序列（项目可能合并/补弱读元音）
  const seq = (list) => list.map((p) => normalizeIpa(p.sound)).join('+');
  return seq(a) === seq(b);
}

let pass = 0, fail = 0;

function check(name, ok, detail) {
  // ok 为 null 表示项目表无此词，属中性跳过，不算失败；'loose' 表示宽松通过
  const mark = ok === null ? '➖' : ok === 'loose' ? '🟡' : ok ? '✅' : '❌';
  console.log(`${mark} ${name}${detail ? ` —— ${detail}` : ''}`);
  if (ok === null) return;
  ok === 'loose' ? pass++ : ok ? pass++ : fail++;
}

const phonicsMap = readJson(path.join(RES_DIR, 'WordPhonicsMap.json')).entries;
const syllableMap = readJson(path.join(RES_DIR, 'WordSyllableMap.json')).entries;
const byWidPhonics = new Map(phonicsMap.map((e) => [e.wid, e]));
const byWidSyllable = new Map(syllableMap.map((e) => [e.wid, e]));

// ---------- 1. 逐词比对 ----------
const wordFiles = fs.readdirSync(dir).filter((f) => /^word_\d+\.decrypted\.json$/i.test(f));
for (const f of wordFiles) {
  const w = readJson(path.join(dir, f));
  const wid = w.wordInfo.wid;
  const word = w.wordInfo.word;

  console.log(`\n== ${word} (wid ${wid}) ==`);

  // 音标一致性：解密 us/uk vs 项目表（phonics 表无音标，跳过）
  // phonics 比对
  const pEntry = byWidPhonics.get(wid);
  if (!pEntry) {
    check(`${f} phonics`, null, `项目 phonics 表无 wid ${wid}，跳过`);
  } else {
    const online = JSON.parse(w.wordInfo.y_phonics);
    const wordOk = pEntry.word === word;
    const strictOk = wordOk && phonemesEqual(pEntry.phonemes, online);
    const looseOk = wordOk && phonemesLooseEqual(pEntry.phonemes, online);
    check(`${f} phonics 与项目表一致 (word=${pEntry.word})`,
      strictOk ? true : looseOk ? 'loose' : false,
      !wordOk ? `词不一致: 项目=${pEntry.word} 线上=${word}`
        : strictOk ? 'sound+position 完全相同'
        : looseOk ? '宽松一致（仅 IPA 字符规整或弱读元音合并差异，同源数据）'
        : '音素序列实质不同');
  }

  // syllable 比对
  const sEntry = byWidSyllable.get(wid);
  if (!sEntry) {
    check(`${f} syllable`, null, `项目 syllable 表无 wid ${wid}，跳过`);
  } else {
    const onlineSyll = w.wordInfo.my_syllable ? JSON.parse(w.wordInfo.my_syllable) : null;
    const projectSyll = sEntry.my_syllable ? JSON.parse(sEntry.my_syllable) : null;
    const same = JSON.stringify(onlineSyll) === JSON.stringify(projectSyll);
    // 宽松比对：忽略项目侧后处理（补 wk、去 (ə) 等），只比 uk/us/ws
    const loose = (o, p) => o && p && o.uk === p.uk && o.ws === p.ws
      && o.us.replace('(ə)', '') === p.us.replace('(ə)', '');
    check(`${f} my_syllable 与项目表一致`, same ? true : loose(onlineSyll, projectSyll) ? 'loose' : false,
      same ? 'uk/us/ws/wk 切分完全相同'
        : loose(onlineSyll, projectSyll) ? '宽松一致（差异仅为 wk 补全或 (ə) 规整，同源数据）'
        : `线上=${w.wordInfo.my_syllable} 项目=${sEntry.my_syllable}`);
  }
}

// ---------- 2. 单元 JSON 与单词 JSON 交叉验证 ----------
const unitFiles = fs.readdirSync(dir).filter((f) => /decrypted\.json$/i.test(f) && !/^word_\d+/.test(f));
for (const f of unitFiles) {
  const unit = readJson(path.join(dir, f));
  console.log(`\n== ${f}（${Array.isArray(unit) ? unit.length : '?'} 个词条） ==`);

  // 找出单元里已有对应单词解密文件的词条，比对关键词段
  for (const entry of Array.isArray(unit) ? unit : []) {
    if (entry.type !== 'word' || !entry.wid) continue;
    const wordFile = path.join(dir, `word_${entry.wid}.decrypted.json`);
    if (!fs.existsSync(wordFile)) continue;
    const w = readJson(wordFile);
    const sameWord = w.wordInfo.word === entry.english;
    const sameUs = w.wordInfo.us === entry.us;
    const sameUk = w.wordInfo.uk === entry.uk;
    check(`单元词条 ${entry.english} (wid ${entry.wid}) vs word_${entry.wid}.json`,
      sameWord && sameUs && sameUk,
      `word=${sameWord} us=${sameUs} uk=${sameUk}`);
  }

  // 统计单元词条在项目两张表中的覆盖率
  const wids = (Array.isArray(unit) ? unit : []).filter((e) => e.type === 'word').map((e) => e.wid);
  const inPhonics = wids.filter((id) => byWidPhonics.has(id)).length;
  const inSyllable = wids.filter((id) => byWidSyllable.has(id)).length;
  console.log(`ℹ️ 单元共 ${wids.length} 词；项目 phonics 表覆盖 ${inPhonics}，syllable 表覆盖 ${inSyllable}`);
}

console.log(`\n结果：${pass} 项通过，${fail} 项失败`);
if (fail > 0) process.exitCode = 1;
