#!/usr/bin/env node
/**
 * 下载 + 解密脚本（固定前缀版）
 *
 * 固定前缀（传入的数据直接拼在后面）：
 *   单元：https://static.jtsee.com/web/json/courses/<传入的数据>.json
 *   单词：https://static.jtsee.com/web/json/words/<传入的数据>.json
 *
 * 用法：
 *   node fetch-decrypt.js <传入的数据...>
 *
 * 规则：
 *   - word 开头的数据走 words/，其余先试 courses/ 再回退 words/；
 *   - 可一次传多个数据，也可用 `-` 从 stdin 批量接收（空格/换行分隔）；
 *   - 输出保存到当前目录 <数据>.decrypted.json；
 *   - 默认不覆盖已有输出，加 --force 可覆盖；
 *   - 需要 Node.js >= 18（内置 fetch）。
 *
 * 示例：
 *   node fetch-decrypt.js superword_book872_Unit_2
 *   node fetch-decrypt.js word_3357
 *   node fetch-decrypt.js word_3357 word_3358 word_3359 --force
 *   echo "word_3357 word_3358" | node fetch-decrypt.js -
 */
const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');

const BASE_COURSE = 'https://static.jtsee.com/web/json/courses/';
const BASE_WORD = 'https://static.jtsee.com/web/json/words/';

const KEY = Buffer.from('supersoftwordbook' + '1234ruanshukeji', 'utf8');
const ENCRYPTED_PATTERN = /^([0-9a-fA-F]{32}):([0-9a-fA-F]+)$/;

function decodeCourseJson(raw) {
  const input = raw.trim();

  // 普通 JSON 直接解析。
  try {
    return JSON.parse(input);
  } catch {
    // 继续检查 IV:密文格式。
  }

  const match = ENCRYPTED_PATTERN.exec(input);
  if (!match || match[2].length % 32 !== 0) {
    throw new Error('内容既不是有效 JSON，也不是 IV:十六进制密文格式');
  }

  try {
    const decipher = crypto.createDecipheriv(
      'aes-256-cbc', KEY, Buffer.from(match[1], 'hex')
    );
    const plain = Buffer.concat([
      decipher.update(Buffer.from(match[2], 'hex')),
      decipher.final(),
    ]).toString('utf8');
    return JSON.parse(plain);
  } catch (error) {
    throw new Error(`解密或解析失败：${error.message}`);
  }
}

/** 把传入的数据解析成按固定前缀拼出的待尝试 URL 列表。 */
function resolveTargets(arg) {
  // 去掉可能误带的 .json 后缀和完整前缀（容错：有人把整段 URL 粘进来也照样拆出数据）
  let id = arg.replace(/\.json$/i, '');
  id = id.replace(/^https?:\/\/static\.jtsee\.com\/web\/json\/(courses|words)\//i, '');
  if (!id || /[\\/]/.test(id)) throw new Error(`无效数据：${arg}`);

  const make = (base) => ({ label: base + id + '.json', url: base + id + '.json', name: id });

  if (/^word/i.test(id)) {
    // 单词数据：直接走 words
    return [make(BASE_WORD)];
  }

  // 其余视为单元数据：先 courses，再回退 words
  return [make(BASE_COURSE), make(BASE_WORD)];
}

async function fetchText(url) {
  const res = await fetch(url, {
    headers: {
      'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
      'Accept': '*/*',
    },
  });
  if (!res.ok) throw new Error(`HTTP ${res.status} ${res.statusText}`);
  return res.text();
}

async function processOne(arg, outDir, force) {
  const targets = resolveTargets(arg);
  let lastError = null;

  for (const target of targets) {
    try {
      const raw = await fetchText(target.url);
      const data = decodeCourseJson(raw);
      const outputPath = path.join(outDir, `${target.name}.decrypted.json`);

      if (!force && fsSync.existsSync(outputPath)) {
        console.log(`跳过（已存在，加 --force 可覆盖）：${outputPath}`);
        return;
      }

      await fs.writeFile(outputPath, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
      console.log(`✅ ${target.label}\n   -> ${outputPath}`);
      return;
    } catch (error) {
      lastError = error;
      // 多目标时静默回退下一个前缀。
    }
  }

  throw new Error(`${arg} 处理失败：${lastError ? lastError.message : '未知错误'}`);
}

// 同步 existsSync 用于输出覆盖判断。
const fsSync = require('node:fs');

async function main() {
  let args = process.argv.slice(2);

  // `-` 表示从 stdin 批量接收数据（其他脚本/网页可管道传入）
  if (args.includes('-')) {
    const stdin = require('node:fs').readFileSync(0, 'utf8');
    args = args.filter((a) => a !== '-')
      .concat(stdin.split(/\s+/).filter(Boolean));
  }

  const force = args.includes('--force');
  const positional = args.filter((a) => a !== '--force');

  if (positional.length === 0) {
    console.log('用法：node fetch-decrypt.js <传入的数据...>\n'
      + '固定前缀：\n'
      + '  https://static.jtsee.com/web/json/courses/<数据>.json\n'
      + '  https://static.jtsee.com/web/json/words/<数据>.json\n'
      + '示例：\n'
      + '  node fetch-decrypt.js superword_book872_Unit_2\n'
      + '  node fetch-decrypt.js word_3357\n'
      + '  node fetch-decrypt.js word_3357 word_3358 --force\n'
      + '  echo "word_3357 word_3358" | node fetch-decrypt.js -');
    return;
  }

  if (typeof fetch !== 'function') {
    throw new Error('需要 Node.js >= 18（内置 fetch）');
  }

  const outDir = process.cwd();
  for (const arg of positional) {
    await processOne(arg, outDir, force);
  }
}

main().catch((error) => {
  console.error(`错误：${error.message}`);
  process.exitCode = 1;
});
