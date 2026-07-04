// Cookie Clicker Blazor Remake — small JS helpers
//
// Kept out of blazor.webassembly.js so we can iterate on them without
// touching framework files. Loaded from index.html as a plain <script>.

(function () {
    'use strict';

    // ---- Web Audio: synth beeps (no external assets) --------------------
    //
    // We synthesise short tones via the WebAudio API instead of shipping
    // .mp3 / .ogg files. That keeps the repo tiny and dodges licensing
    // questions. Sounds are intentionally muted-friendly: everything routes
    // through a master gain that the C# side can flip to 0.
    let audioCtx = null;
    let masterGain = null;

    function ensureAudio() {
        if (audioCtx) return audioCtx;
        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return null;
            audioCtx = new Ctx();
            masterGain = audioCtx.createGain();
            masterGain.gain.value = 0.35;
            masterGain.connect(audioCtx.destination);
        } catch (e) {
            audioCtx = null;
        }
        return audioCtx;
    }

    function playTone(frequency, durationMs, type, gain) {
        const ctx = ensureAudio();
        if (!ctx || !masterGain) return;
        // On Safari / mobile audio contexts start suspended until a user gesture.
        if (ctx.state === 'suspended') { try { ctx.resume(); } catch (e) { /* ignore */ } }

        const osc = ctx.createOscillator();
        const g = ctx.createGain();
        osc.type = type || 'sine';
        osc.frequency.value = frequency;
        const now = ctx.currentTime;
        const dur = Math.max(0.02, (durationMs || 60) / 1000);
        // Cheap ADSR: quick attack, exponential decay to silence.
        g.gain.setValueAtTime(0.0001, now);
        g.gain.exponentialRampToValueAtTime(gain || 0.9, now + 0.005);
        g.gain.exponentialRampToValueAtTime(0.0001, now + dur);
        osc.connect(g).connect(masterGain);
        osc.start(now);
        osc.stop(now + dur + 0.05);
    }

    window.cookieClicker = window.cookieClicker || {};

    window.cookieClicker.setMuted = function (muted) {
        const ctx = ensureAudio();
        if (!ctx || !masterGain) return;
        masterGain.gain.value = muted ? 0 : 0.35;
    };

    window.cookieClicker.playClick = function () {
        // Slightly randomised so mashing doesn't sound robotic.
        const base = 620 + Math.random() * 40;
        playTone(base, 55, 'triangle', 0.7);
    };

    window.cookieClicker.playPurchase = function () {
        // Two-tone chirp: little melodic sting.
        playTone(520, 70, 'sine', 0.6);
        setTimeout(() => playTone(780, 90, 'sine', 0.5), 55);
    };

    window.cookieClicker.playGolden = function () {
        // Rising three-tone arpeggio.
        playTone(660, 90, 'sine', 0.6);
        setTimeout(() => playTone(880, 90, 'sine', 0.55), 80);
        setTimeout(() => playTone(1180, 140, 'triangle', 0.5), 170);
    };

    window.cookieClicker.playAchievement = function () {
        // Warm bell.
        playTone(1046, 200, 'sine', 0.5);
        setTimeout(() => playTone(1568, 260, 'sine', 0.35), 90);
    };

    window.cookieClicker.playAscend = function () {
        // Long shimmery upsweep, used sparingly.
        playTone(392, 220, 'sine', 0.5);
        setTimeout(() => playTone(587, 220, 'sine', 0.45), 180);
        setTimeout(() => playTone(784, 320, 'triangle', 0.4), 360);
        setTimeout(() => playTone(1174, 420, 'sine', 0.35), 560);
    };

})();
