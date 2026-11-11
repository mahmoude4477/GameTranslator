#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

echo "================================================"
echo "      🎮 مشغّل GameTranslator (macOS)"
echo "================================================"

# إذا أُعطي الخيار --rebuild أو لم يكن التطبيق مثبتًا بعد، فابنِه تلقائيًا
if [ "$1" == "--rebuild" ] || [ "$1" == "--build" ] || { [ ! -d "/Applications/GameTranslator.app" ] && [ ! -d "$DIR/dist/GameTranslator.app" ]; }; then
    echo "⚙️ جارٍ بناء GameTranslator وتثبيته..."
    echo "يرجى الانتظار (سيستغرق بضع ثوانٍ)..."
    bash "$DIR/mac/build.sh"
    echo ""
fi

# شغّل التطبيق
if [ -d "/Applications/GameTranslator.app" ]; then
    echo "🚀 جارٍ تشغيل GameTranslator..."
    open "/Applications/GameTranslator.app"
elif [ -d "$DIR/dist/GameTranslator.app" ]; then
    echo "🚀 جارٍ تشغيل GameTranslator..."
    open "$DIR/dist/GameTranslator.app"
else
    echo "❌ خطأ: تعذّر العثور على GameTranslator.app وتعذّر بناؤه."
    echo "يرجى مراجعة مخرجات build.sh في مجلد mac/."
    read -p "اضغط Enter للإغلاق..."
    exit 1
fi

echo "✅ تم فتح GameTranslator بنجاح. لعبًا ممتعًا!"
sleep 1
