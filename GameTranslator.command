#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

echo "================================================"
echo "      🎮 GameTranslator Başlatıcı (macOS)"
echo "================================================"

# Eğer --rebuild parametresi verildiyse veya uygulama henüz kurulmamışsa otomatik derle
if [ "$1" == "--rebuild" ] || [ "$1" == "--build" ] || { [ ! -d "/Applications/GameTranslator.app" ] && [ ! -d "$DIR/dist/GameTranslator.app" ]; }; then
    echo "⚙️ GameTranslator derleniyor ve kuruluyor..."
    echo "Lütfen bekleyin (birkaç saniye sürecek)..."
    bash "$DIR/mac/build.sh"
    echo ""
fi

# Uygulamayı başlat
if [ -d "/Applications/GameTranslator.app" ]; then
    echo "🚀 GameTranslator başlatılıyor..."
    open "/Applications/GameTranslator.app"
elif [ -d "$DIR/dist/GameTranslator.app" ]; then
    echo "🚀 GameTranslator başlatılıyor..."
    open "$DIR/dist/GameTranslator.app"
else
    echo "❌ Hata: GameTranslator.app bulunamadı ve derlenemedi."
    echo "Lütfen mac/ klasöründeki build.sh çıktısını kontrol edin."
    read -p "Kapatmak için Enter'a basın..."
    exit 1
fi

echo "✅ GameTranslator başarıyla açıldı. İyi oyunlar!"
sleep 1
