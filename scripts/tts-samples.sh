#!/usr/bin/env bash
# Türkçe dublaj denemesi (.github/workflows/tts-samples.yml): her sesle aynı replikleri seslendirir ve süreleri yazar.
# Kullanım: tts-samples.sh <pocket-tts programı> <ayar ve seslerin klasörü> <çıktı klasörü>
set -euo pipefail
BIN=$1 DIR=$2 OUT=$3
mkdir -p "$OUT"

# Oyun replikleri (GameTranslator için yazıldı). Cümleler küçük harfle başlar; duygu etiketi en başta.
LINES=(
  "notr|nehrin karşı tarafına geçmemiz gerek, köprü yıkılmış."
  "kizgin|[kızgın] geri çekil! bu savaş seninle ilgili değil."
  "uzgun|[üzgün] annen bunu görseydi, seninle gurur duyardı."
  "saskin|[şaşkın] bu da ne? kapı kendi kendine açıldı!"
  "sakin|[sakin] nefes al, oğlum. acele etme, iz sürmeyi öğreneceksin."
  "mutlu|[mutlu] başardık! sonunda eve dönüyoruz."
)

seconds() { python3 -c "import wave,sys;w=wave.open(sys.argv[1]);print(round(w.getnframes()/w.getframerate(),2))" "$1"; }

# İlk çağrı modeli indirir; süre ölçümüne katılmaz.
"$BIN" generate --config "$DIR/tr.yaml" --voice "$DIR/female_1.wav" --text "merhaba." --output-path "$OUT/isinma.wav" --quiet
rm -f "$OUT/isinma.wav"

echo "dosya | ses süresi (sn) | üretim süresi (sn)" > "$OUT/sureler.txt"
for quantize in no-quantize quantize; do
  for voice in female_1 female_2 female_3 male_1 male_2 male_3; do
    for entry in "${LINES[@]}"; do
      name=${entry%%|*} text=${entry#*|}
      # Hız ölçümü için int8 yalnız iki sesle denenir.
      [[ $quantize == quantize && $voice != female_1 && $voice != male_1 ]] && continue
      file="$OUT/$quantize/$voice-$name.wav"
      mkdir -p "$OUT/$quantize"
      start=$(python3 -c "import time;print(time.time())")
      "$BIN" generate --"$quantize" --config "$DIR/tr.yaml" --voice "$DIR/$voice.wav" --text "$text" --output-path "$file" --quiet
      took=$(python3 -c "import time,sys;print(round(time.time()-float(sys.argv[1]),2))" "$start")
      echo "$quantize/$voice-$name.wav | $(seconds "$file") | $took" >> "$OUT/sureler.txt"
    done
  done
done
cat "$OUT/sureler.txt"
