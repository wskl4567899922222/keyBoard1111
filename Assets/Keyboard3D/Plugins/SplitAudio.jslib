// 音节拆分音频的 WebGL 专用播放通道：
// Unity 的 AudioClip.GetData 对 CompressedInMemory clip 会失败、AudioSource.time seek 在 WebGL 上不可靠，
// 而浏览器 Web Audio API 原生支持 offset 播放（AudioBufferSourceNode.start(0, offset, duration)）。
// 注意 1：本工程模板未暴露全局 unityInstance → jslib 内不能用 SendMessage 回调 C#，
//         解码完成后直接在 JS 侧播放（无回调）。C# 侧只用一个入口 SplitAudioPlayAsync。
// 注意 2：mergeInto 外部的顶层函数不会进入导出函数的作用域（实测 "__splitAudioPlay is not defined"），
//         所以播放逻辑必须写成 SplitAudioPlayAsync 内部的局部闭包。

mergeInto(LibraryManager.library, {

    SplitAudioPlayAsync: function (urlPtr, keyPtr, offset, dur, volume) {
        var url = UTF8ToString(urlPtr);
        var key = UTF8ToString(keyPtr);
        var SA = window.__splitAudio;
        if (!SA) {
            SA = window.__splitAudio = { ctx: null, bufs: {}, failed: {}, pending: {}, current: null, lastKey: null, lastPlayAt: -1 };
            var Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) { SA.failed[key] = true; return; }
            SA.ctx = new Ctx();
            // 自动解锁：首次按键/点击时恢复上下文（与 index.html 的键盘解锁补丁同一策略）
            var resume = function () { if (SA.ctx && SA.ctx.state === 'suspended') SA.ctx.resume(); };
            document.addEventListener('keydown', resume, true);
            document.addEventListener('pointerdown', resume, true);
        }
        if (SA.failed[key]) return;
        // 播放（局部闭包，避免 mergeInto 作用域陷阱）
        var play = function () {
            if (!SA.bufs[key]) return;
            if (SA.ctx.state === 'suspended') SA.ctx.resume();
            // 同一 key 在其时长内不重复触发（防同一击键的双重调用叠音）
            if (SA.lastKey === key && SA.lastPlayAt >= 0 && SA.ctx.currentTime - SA.lastPlayAt < dur) return;
            // 同一时间只播一个音节片段：新片段打断上一个（与 splitSource 单音源行为一致）
            if (SA.current) { try { SA.current.stop(0); } catch (e) { } SA.current = null; }
            var src = SA.ctx.createBufferSource();
            src.buffer = SA.bufs[key];
            var gain = SA.ctx.createGain();
            gain.gain.value = volume;
            src.connect(gain);
            gain.connect(SA.ctx.destination);
            src.start(0, offset, dur);
            SA.current = src;
            SA.lastKey = key;
            SA.lastPlayAt = SA.ctx.currentTime;
        };
        if (SA.bufs[key]) { play(); return; }
        if (SA.pending[key]) return; // 解码中：本次忽略（解码完自动播首次的 offset/dur）
        SA.pending[key] = true;
        if (SA.ctx.state === 'suspended') SA.ctx.resume();
        fetch(url)
            .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.arrayBuffer(); })
            .then(function (ab) { return SA.ctx.decodeAudioData(ab); })
            .then(function (buf) {
                SA.bufs[key] = buf;
                delete SA.pending[key];
                play();
            })
            .catch(function (e) {
                delete SA.pending[key];
                SA.failed[key] = true;
                console.log('[SplitAudio] 解码失败 ' + key + ' : ' + (e && e.message ? e.message : e));
            });
    }

});
