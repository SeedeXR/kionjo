"""Word timings of a voice line for Kionjo's Words trim mode.

    python -I kionjo_words.py <audio> [--model base.en] [--ffmpeg ffmpeg]

Prints JSON: {"words": [{"w": "Human,", "s": 0.12, "e": 0.41}, ...]}
Exits 3 with {"error": "..."} when faster-whisper is not installed.

The audio is decoded by ffmpeg into 16 kHz mono float32 and handed to faster-whisper as an
array: decoding through PyAV failed on a version mismatch in Baraza's trailer 1.
"""
import argparse
import json
import subprocess
import sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("audio")
    ap.add_argument("--model", default="base.en")
    ap.add_argument("--ffmpeg", default="ffmpeg")
    args = ap.parse_args()
    try:
        import numpy as np
        from faster_whisper import WhisperModel
    except ImportError as e:
        print(json.dumps({"error": f"faster-whisper is not installed ({e}). pip install faster-whisper"}))
        return 3

    raw = subprocess.run(
        [args.ffmpeg, "-v", "error", "-nostdin", "-i", args.audio, "-f", "f32le", "-ac", "1", "-ar", "16000", "-"],
        stdout=subprocess.PIPE, check=True).stdout
    audio = np.frombuffer(raw, dtype=np.float32)
    model = WhisperModel(args.model, device="cpu", compute_type="int8")
    segments, _ = model.transcribe(audio, word_timestamps=True, vad_filter=False)
    words = []
    for seg in segments:
        for w in seg.words or []:
            words.append({"w": w.word.strip(), "s": round(float(w.start), 3), "e": round(float(w.end), 3)})
    print(json.dumps({"words": words}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
