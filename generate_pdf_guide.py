
import os, pathlib, shutil, subprocess, sys, tempfile

html_content = '''<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
<meta charset="UTF-8">
<title>دليل تثبيت تطبيق GameTranslator على التلفزيون</title>
<style>
  @page {
    size: A4;
    margin: 14mm 14mm 14mm 14mm;
  }
  * {
    box-sizing: border-box;
    margin: 0;
    padding: 0;
  }
  body {
    font-family: "Noto Naskh Arabic", "Noto Sans Arabic", "Geeza Pro", "Segoe UI", Tahoma, Arial, sans-serif;
    color: #1f2937;
    background-color: #ffffff;
    direction: rtl;
    text-align: right;
    line-height: 1.6;
    font-size: 12.5px;
  }

  .page-break {
    page-break-before: always;
    break-before: page;
  }

  /* Latin runs (commands, paths, addresses) stay left-to-right inside the Arabic text */
  code, .ltr {
    direction: ltr;
    unicode-bidi: isolate;
  }

  /* Header */
  .header {
    border-bottom: 2px solid #0A84FF;
    padding-bottom: 12px;
    margin-bottom: 16px;
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
  }
  .title-group h1 {
    font-size: 21px;
    font-weight: 800;
    color: #0F172A;
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .title-group .subtitle {
    font-size: 12.5px;
    color: #0A84FF;
    font-weight: 600;
    margin-top: 3px;
  }
  .badge-repo {
    background: #0F172A;
    color: #38BDF8;
    padding: 4px 10px;
    border-radius: 6px;
    font-size: 10px;
    font-family: ui-monospace, SFMono-Regular, monospace;
    font-weight: 600;
    direction: ltr;
    white-space: nowrap;
  }

  /* Intro Box */
  .intro-box {
    background: #F0F7FF;
    border-right: 4px solid #0A84FF;
    padding: 10px 14px;
    border-radius: 8px 0 0 8px;
    margin-bottom: 16px;
    font-size: 11.5px;
    color: #1E3A8A;
  }
  .intro-box strong {
    color: #1D4ED8;
  }

  /* Section */
  .section {
    margin-bottom: 18px;
    break-inside: avoid;
  }
  .section-title {
    font-size: 14.5px;
    font-weight: 700;
    color: #0F172A;
    margin-bottom: 10px;
    display: flex;
    align-items: center;
    gap: 7px;
    border-bottom: 1px solid #E2E8F0;
    padding-bottom: 5px;
  }
  .section-title span.num {
    background: #0A84FF;
    color: white;
    width: 22px;
    height: 22px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    font-weight: 700;
  }

  /* Table */
  table {
    width: 100%;
    border-collapse: collapse;
    margin: 8px 0 14px 0;
    font-size: 11px;
    border-radius: 8px;
    overflow: hidden;
    border: 1px solid #E2E8F0;
  }
  th {
    background: #F1F5F9;
    color: #334155;
    font-weight: 700;
    text-align: right;
    padding: 8px 10px;
    border-bottom: 1px solid #CBD5E1;
  }
  td {
    padding: 7px 10px;
    border-bottom: 1px solid #E2E8F0;
    color: #334155;
    text-align: right;
  }
  tr:last-child td {
    border-bottom: none;
  }
  tr:nth-child(even) {
    background: #F8FAFC;
  }
  .status-yes {
    color: #059669;
    font-weight: 700;
  }
  .status-no {
    color: #DC2626;
    font-weight: 600;
  }

  /* Steps List */
  .step-card {
    background: #F8FAFC;
    border: 1px solid #E2E8F0;
    border-radius: 8px;
    padding: 11px 14px;
    margin-bottom: 10px;
    break-inside: avoid;
  }
  .step-card h3 {
    font-size: 12.5px;
    font-weight: 700;
    color: #0F172A;
    margin-bottom: 6px;
    display: flex;
    align-items: center;
    gap: 6px;
  }
  .step-card ol, .step-card ul {
    margin-right: 20px;
    color: #374151;
    font-size: 11.5px;
  }
  .step-card li {
    margin-bottom: 4px;
  }
  .step-card li:last-child {
    margin-bottom: 0;
  }

  /* Callouts & Badges */
  .tip-box {
    background: #ECFDF5;
    border: 1px solid #A7F3D0;
    border-right: 4px solid #10B981;
    padding: 8px 12px;
    border-radius: 6px 0 0 6px;
    font-size: 11px;
    color: #065F46;
    margin: 8px 0;
  }
  .warn-box {
    background: #FFFBEB;
    border: 1px solid #FDE68A;
    border-right: 4px solid #F59E0B;
    padding: 8px 12px;
    border-radius: 6px 0 0 6px;
    font-size: 11px;
    color: #92400E;
    margin: 8px 0;
  }
  code {
    background: #F1F5F9;
    color: #0F172A;
    padding: 1px 5px;
    border-radius: 4px;
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, "DejaVu Sans Mono", monospace;
    font-size: 10.5px;
    border: 1px solid #E2E8F0;
  }

  /* Two Column */
  .two-cols {
    display: flex;
    gap: 12px;
    margin-bottom: 10px;
  }
  .two-cols > div {
    flex: 1;
  }

  /* Footer */
  .footer {
    margin-top: 20px;
    border-top: 1px solid #E2E8F0;
    padding-top: 10px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 12px;
    font-size: 10px;
    color: #64748B;
  }
  .footer-link {
    white-space: nowrap;
  }
</style>
</head>
<body>

  <!-- الصفحة 1 -->
  <div class="header">
    <div class="title-group">
      <h1>🎮 دليل تثبيت تطبيق GameTranslator على التلفزيون</h1>
      <div class="subtitle">ترجمة نصية عربية فورية لألعاب PlayStation 5 — النسخة العربية</div>
    </div>
    <div class="badge-repo">github.com/mahmoude4477/GameTranslator</div>
  </div>

  <div class="intro-box">
    <strong>معلومة مهمة:</strong> لا تحتاج إلى البحث عن ملفات خارجية أو معقدة لتلفزيونك. تطبيق <strong>GameTranslator</strong> على الكمبيوتر يتصل بالتلفزيون عبر الشبكة ويثبّت تطبيق التلفزيون بنفسه، والحزم الجاهزة موجودة أيضًا في المجلد <code dir="ltr">tv/packages/</code> لمن يريد التثبيت اليدوي. كل ما عليك هو تفعيل وضع المطوّر في التلفزيون مرة واحدة؛ وهو مجاني تمامًا ولا يضر التلفزيون.<br>
    <strong>القاعدة الأساسية:</strong> يجب أن يكون الكمبيوتر والتلفزيون على <u>الشبكة المنزلية نفسها (الراوتر نفسه أو شبكة Wi-Fi نفسها)</u>. ويحتاج الكمبيوتر إلى اتصال بالإنترنت أثناء اللعب، لأن الترجمة إلى العربية تجري عبر الإنترنت (Google Translate افتراضيًا، أو DeepL بمفتاحك الخاص).
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">1</span>
      <span>نزّل تطبيق الكمبيوتر وشغّله</span>
    </div>
    <div class="step-card">
      <ul>
        <li><strong>🍏 Mac (معالجات Apple Silicon: M1 / M2 / M3 / M4):</strong> انقر مرتين على الملف <code dir="ltr">GameTranslator.command</code> في مجلد المشروع. يبني المشغّل التطبيق في المرة الأولى ثم يفتحه مباشرة.</li>
        <li><strong>🪟 Windows (نظام Windows 10 / 11):</strong> انقر مرتين على الملف <code dir="ltr">GameTranslator.bat</code> في مجلد المشروع (يحتاج إلى <span class="ltr" dir="ltr">.NET 10 SDK</span>، وتفتح بعده واجهة سطح المكتب). وإن أردت ملف EXE مستقلًا فشغّل <code dir="ltr">Build-Windows.bat</code>.</li>
        <li><strong>🔗 مستودع المشروع المفتوح المصدر:</strong> <a class="ltr" dir="ltr" href="https://github.com/mahmoude4477/GameTranslator" style="color:#0A84FF; text-decoration:none; font-weight:600;">https://github.com/mahmoude4477/GameTranslator</a></li>
        <li><strong>المشروع الأصلي:</strong> GameTranslator من تطوير Sefflex — <a class="ltr" dir="ltr" href="https://github.com/Sefflex/GameTranslator" style="color:#0A84FF; text-decoration:none; font-weight:600;">https://github.com/Sefflex/GameTranslator</a></li>
      </ul>
    </div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">2</span>
      <span>هل تلفزيونك مدعوم؟ (جدول توافق التلفزيونات)</span>
    </div>
    <table>
      <thead>
        <tr>
          <th>طراز التلفزيون / نظام التشغيل</th>
          <th>حالة الدعم</th>
          <th>طريقة التثبيت</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><strong>Android TV / Google TV</strong><br><small>TCL و Philips و Sony و Xiaomi و Vestel و Beko و Grundig وغيرها</small></td>
          <td class="status-yes">✓ مدعوم بالكامل</td>
          <td>من GameTranslator عبر الشبكة (ADB)، أو ملف APK بذاكرة USB</td>
        </tr>
        <tr>
          <td><strong>LG Smart TV</strong><br><small>webOS 4.0 وما بعده (كل طرازات OLED و QNED و NanoCell منذ 2018)</small></td>
          <td class="status-yes">✓ مدعوم بالكامل</td>
          <td>حزمة IPK مع تطبيق LG Developer Mode</td>
        </tr>
        <tr>
          <td><strong>Samsung Smart TV</strong><br><small>Tizen OS (كل طرازات QLED و Crystal UHD منذ 2019)</small></td>
          <td class="status-yes">✓ مدعوم بالكامل</td>
          <td>حزمة WGT مع وضع المطوّر (الأرقام 12345 من الريموت)</td>
        </tr>
        <tr>
          <td><strong>تلفزيونات Linux وغيرها</strong><br><small>طرازات Linux القديمة من Vestel و Beko، و Philips Saphi، و Titan OS وغيرها</small></td>
          <td class="status-no">✕ غير مدعوم</td>
          <td>لا يمكن عرض طبقة فوق صورة HDMI (تُتابَع الترجمة من شاشة الكمبيوتر أو الهاتف)</td>
        </tr>
      </tbody>
    </table>
    <div class="tip-box">
      <strong>هل تلفزيوني يعمل بنظام Android؟</strong> إذا ظهر في القائمة الرئيسية للتلفزيون <em>Google Play Store</em> أو <em>Google TV</em> فهو يعمل بنظام Android. وإن لم يكن فيه إلا متجر الشركة المصنّعة فهو غالبًا يعمل بنظام Linux أو بنظام خاص.
    </div>
  </div>

  <!-- الصفحة 2 -->
  <div class="page-break"></div>

  <div class="header">
    <div class="title-group">
      <h1>📱 التثبيت على Android TV و Google TV</h1>
      <div class="subtitle">TCL و Philips و Sony و Xiaomi و Vestel وبقية طرازات Android TV / Google TV</div>
    </div>
    <div class="badge-repo">Android TV / Google TV</div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">1</span>
      <span>فعّل وضع تصحيح الأخطاء (ADB) في التلفزيون</span>
    </div>
    <div class="step-card">
      <ol>
        <li>من ريموت التلفزيون افتح <strong>«الإعدادات» ← «تفضيلات الجهاز» ← «لمحة»</strong> (في طرازات Google TV: <strong>«الإعدادات» ← «النظام» ← «لمحة»</strong>). بالإنجليزية: <code dir="ltr">Settings › Device Preferences › About</code>.</li>
        <li>ابحث في القائمة عن <strong>«رقم الإصدار» (Build Number)</strong> واضغط زر <strong>«موافق» (OK)</strong> في الريموت <strong>7 مرات</strong> متتالية. تظهر على الشاشة رسالة <em>«أنت الآن مطوّر!»</em>.</li>
        <li>ارجع إلى القائمة السابقة وادخل إلى <strong>«خيارات المطوّرين» (Developer options)</strong>.</li>
        <li>فعّل خيار <strong>«تصحيح أخطاء USB» (USB debugging)</strong>. وإن وجدت في القائمة خيار <strong>«تصحيح الأخطاء عبر الشبكة» (Network debugging)</strong> ففعّله أيضًا.</li>
      </ol>
      <div class="warn-box">
        <em>ملاحظة:</em> قد تختلف أسماء القوائم قليلًا حسب ماركة التلفزيون وإصدار واجهته.
      </div>
    </div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">2</span>
      <span>ثبّت تطبيق GameTranslator على التلفزيون</span>
    </div>
    <div class="two-cols">
      <div class="step-card">
        <h3>🟢 الطريقة (أ): تثبيت تلقائي من الكمبيوتر (موصى بها)</h3>
        <ol>
          <li>افتح تطبيق <strong>GameTranslator</strong> على الكمبيوتر، وانتقل في الإعداد إلى <strong>خطوة التلفزيون</strong> واختر Android TV.</li>
          <li>اكتب عنوان IP المحلي للتلفزيون (مثال: <code dir="ltr">192.168.1.108</code>) أو دع التطبيق يبحث عن التلفزيون في الشبكة.</li>
          <li>اضغط زر <strong>التثبيت التلقائي على التلفزيون</strong>.</li>
          <li>يظهر على شاشة التلفزيون السؤال <em>«هل تريد السماح بتصحيح أخطاء USB؟»</em>. ضع علامة على <strong>«السماح دائمًا من هذا الكمبيوتر»</strong> ثم اضغط <strong>«سماح»</strong>.</li>
          <li>يُثبَّت التطبيق على التلفزيون ويُفتح تلقائيًا.</li>
        </ol>
      </div>

      <div class="step-card">
        <h3>💾 الطريقة (ب): التثبيت بذاكرة USB</h3>
        <ol>
          <li>انسخ الملف <code dir="ltr">tv/packages/gametranslator-androidtv_1.0.0.apk</code> من مجلد المشروع إلى ذاكرة USB.</li>
          <li>صِل ذاكرة USB بالتلفزيون.</li>
          <li>افتح ملف APK من أي تطبيق لإدارة الملفات على التلفزيون وأكمل التثبيت.</li>
          <li>افتح تطبيق <strong>GameTranslator</strong> الذي ثُبّت على التلفزيون.</li>
        </ol>
      </div>
    </div>
    <div class="tip-box">
      <strong>التحقق من الصورة:</strong> عند فتح GameTranslator على التلفزيون تظهر صورة اللعبة القادمة من HDMI في الخلفية. إذا بقيت الخلفية سوداء فاضغط زر <strong>«موافق» (OK)</strong> في الريموت لفتح اللوحة، ثم اختر بسهمَي اليمين واليسار منفذ HDMI الموصول به جهاز PS5 (مثل HDMI 1).
    </div>
  </div>

  <!-- الصفحة 3 -->
  <div class="page-break"></div>

  <div class="header">
    <div class="title-group">
      <h1>📺 التثبيت على تلفزيون LG (webOS)</h1>
      <div class="subtitle">webOS 4.0 وما بعده (طرازات OLED و QNED و NanoCell و UHD)</div>
    </div>
    <div class="badge-repo">LG webOS</div>
  </div>

  <div class="section">
    <div class="step-card">
      <h3>1. أنشئ حساب مطوّر LG مجانيًا</h3>
      <ol>
        <li>من متصفح الكمبيوتر افتح العنوان <code dir="ltr">webostv.developer.lge.com</code>.</li>
        <li>اضغط <strong>SIGN IN</strong> في أعلى الصفحة ثم <strong>Create Account</strong> وأنشئ حساب مطوّر مجانيًا.</li>
      </ol>
    </div>

    <div class="step-card">
      <h3>2. شغّل تطبيق Developer Mode على التلفزيون</h3>
      <ol>
        <li>تأكد أن التلفزيون متصل بالإنترنت.</li>
        <li>افتح متجر <strong>LG Content Store</strong> على التلفزيون، واكتب في البحث <strong>"Developer Mode"</strong> وثبّت التطبيق الرسمي.</li>
        <li>افتح تطبيق Developer Mode وسجّل الدخول ببريد حساب LG الذي أنشأته وكلمة مروره.</li>
        <li>اجعل خيار <strong>Dev Mode Status</strong> على <strong>ON</strong>. يُعاد تشغيل التلفزيون تلقائيًا.</li>
      </ol>
    </div>

    <div class="step-card">
      <h3>3. ثبّت تطبيق GameTranslator على التلفزيون</h3>
      <ol>
        <li>بعد إعادة التشغيل افتح تطبيق <strong>Developer Mode</strong> من جديد واجعل خيار <strong>Key Server</strong> على <strong>ON</strong>. اترك التطبيق مفتوحًا على الشاشة.</li>
        <li>يظهر في أسفل يسار شاشة التلفزيون رمز <strong>Passphrase</strong> من 6 أحرف.</li>
        <li>في تطبيق GameTranslator على الكمبيوتر (Windows) اختر تلفزيون LG في خطوة التلفزيون، وأدخل رمز Passphrase مع مراعاة الأحرف الكبيرة والصغيرة، ثم اضغط زر <strong>التثبيت على التلفزيون</strong>.</li>
        <li>يثبّت GameTranslator الحزمة <code dir="ltr">tv/packages/com.gametranslator.tv_1.0.0_all.ipk</code> على التلفزيون ويشغّلها.</li>
      </ol>
      <div class="tip-box">
        <em>ملاحظة عن المدة:</em> يبقى وضع المطوّر في LG مفعّلًا 1000 ساعة. يعيد تطبيق GameTranslator في Windows بدء هذه المدة تلقائيًا في كل مرة تستخدمه. وإن كنت لن تستخدمه مدة طويلة فاضغط <strong>EXTEND</strong> في تطبيق Developer Mode قبل انتهاء المدة.
      </div>
      <div class="warn-box">
        <em>التثبيت اليدوي:</em> إن لم يتوفر التثبيت التلقائي في تطبيق الكمبيوتر لديك (كما في تطبيق Mac) فثبّت الحزمة نفسها ببرنامج <strong>webOS Dev Manager</strong> على الكمبيوتر: اتصل بالتلفزيون بعنوان IP الخاص به ثم اسحب ملف IPK إليه.
      </div>
    </div>
  </div>

  <!-- الصفحة 4 -->
  <div class="page-break"></div>

  <div class="header">
    <div class="title-group">
      <h1>📺 التثبيت على تلفزيون Samsung (Tizen)</h1>
      <div class="subtitle">طرازات Tizen OS منذ 2019</div>
    </div>
    <div class="badge-repo">Samsung Tizen</div>
  </div>

  <div class="section">
    <div class="step-card">
      <h3>1. فعّل وضع المطوّر في التلفزيون</h3>
      <ol>
        <li>افتح شاشة <strong>«التطبيقات» (Apps)</strong> في التلفزيون.</li>
        <li>اضغط بأزرار الأرقام في الريموت <strong>12345</strong> بالترتيب، أي 1 ثم 2 ثم 3 ثم 4 ثم 5 (إن لم تكن في الريموت أزرار أرقام فاكتبها بلوحة المفاتيح على الشاشة).</li>
        <li>تظهر على الشاشة نافذة <strong>Developer Mode</strong>. اجعل <strong>Developer Mode</strong> على <strong>ON</strong>.</li>
        <li>اكتب في خانة <strong>Host PC IP</strong> عنوان IP المحلي للكمبيوتر ثم اضغط <strong>OK</strong> (يعرض تطبيق GameTranslator عنوان IP للكمبيوتر بوضوح في خطوة التلفزيون).</li>
        <li>أعد تشغيل التلفزيون <strong>بالضغط المطوّل على زر التشغيل في الريموت إلى أن يظهر شعار Samsung</strong>. بعد التشغيل ترى في أعلى شاشة التطبيقات عبارة <em>"Develop Mode"</em>.</li>
      </ol>
    </div>

    <div class="step-card">
      <h3>2. ثبّت حزمة التلفزيون من GameTranslator</h3>
      <ol>
        <li>في خطوة التلفزيون داخل GameTranslator على الكمبيوتر (Windows) اختر تلفزيون Samsung واضغط زر <strong>الاتصال</strong>.</li>
        <li>إذا ظهرت على شاشة التلفزيون نافذة إذن الاتصال فاختر <strong>«سماح»</strong> بالريموت.</li>
        <li>تطلب Samsung توقيع التطبيق لكل تلفزيون على حدة، لذلك يفتح GameTranslator صفحة تسجيل الدخول إلى Samsung في المتصفح مرة واحدة. سجّل الدخول بحساب Samsung مجاني.</li>
        <li>يوقّع GameTranslator الحزمة <code dir="ltr">tv/packages/gametranslator-samsung_1.0.0.wgt</code> لتلفزيونك ثم يثبّتها ويشغّلها.</li>
      </ol>
      <div class="warn-box">
        <strong>حل المشكلات:</strong> إذا لم يقبل التلفزيون الكمبيوتر فتأكد أن عنوان Host PC IP يطابق عنوان IP الحالي للكمبيوتر، ثم أعد تشغيل التلفزيون بزر التشغيل في الريموت.
      </div>
    </div>
  </div>

  <div class="section">
    <div class="section-title">
      <span class="num">✨</span>
      <span>كيف أعرف أن التثبيت نجح؟ ومزايا ذكية</span>
    </div>
    <div class="step-card">
      <ul>
        <li><strong>لوحة الحالة والتشخيص:</strong> وتطبيق GameTranslator مفتوح على التلفزيون اضغط زر <strong>«موافق» (OK)</strong> في الريموت فتظهر لوحة المعلومات. ترى فيها <strong>منفذ HDMI</strong> المختار، وعنوان IP للكمبيوتر أمام <strong>«التطبيق»</strong>، وعبارة <strong>«الاتصال: متصل»</strong>. اضغط <strong>«موافق» (OK)</strong> مرة أخرى لإغلاق اللوحة.</li>
        <li><strong>تغطية السطر الإنجليزي تلقائيًا:</strong> عند اختيار وضع تغطية الترجمة الإنجليزية في GameTranslator، يحسب التعرّف على النص (OCR) مكان السطر الإنجليزي وحجمه بدقة، فيُرسم مربّع داكن يغطي النص الأصلي وتوضع الترجمة العربية فوقه تمامًا، من اليمين إلى اليسار.</li>
        <li><strong>كتم صوت اللعبة على الكمبيوتر:</strong> صوت اللعبة يصل أصلًا من التلفزيون عبر HDMI، لذا يمكنك من واجهة GameTranslator كتم صوت الكمبيوتر بنقرة واحدة حتى لا تسمع الصوت مرتين.</li>
      </ul>
    </div>
  </div>

  <div class="footer">
    <div>🎮 GameTranslator — النسخة العربية | مجاني ومفتوح المصدر (AGPL-3.0)<br>المشروع الأصلي: GameTranslator من تطوير Sefflex — <span class="ltr" dir="ltr">github.com/Sefflex/GameTranslator</span></div>
    <div class="footer-link">الدعم والمساهمة: <strong class="ltr" dir="ltr">github.com/mahmoude4477/GameTranslator</strong></div>
  </div>

</body>
</html>
'''

# كل المسارات نسبةً إلى مجلد هذا السكربت (جذر المستودع)
script_dir = os.path.dirname(os.path.abspath(__file__))
pdf_docs = os.path.join(script_dir, 'docs', 'GameTranslator_TV_Setup_Guide.pdf')
pdf_root = os.path.join(script_dir, 'GameTranslator_TV_Setup_Guide.pdf')


def find_browser():
    """يبحث عن متصفح مبني على Chromium يستطيع الطباعة إلى PDF."""
    # 1. متغيّر البيئة CHROME
    env_browser = os.environ.get('CHROME')
    if env_browser:
        found = env_browser if os.path.isfile(env_browser) else shutil.which(env_browser)
        if found:
            return found
        print('Warning: CHROME is set but was not found:', env_browser, file=sys.stderr)
    # 2. مسار Google Chrome في macOS
    mac_chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
    if os.path.isfile(mac_chrome):
        return mac_chrome
    # 3. الأسماء المعتادة في PATH
    for name in ('chromium', 'chromium-browser', 'google-chrome'):
        found = shutil.which(name)
        if found:
            return found
    return None


def main():
    browser = find_browser()
    if not browser:
        sys.exit('No Chrome/Chromium browser found. Install Chromium or Google Chrome, '
                 'or set the CHROME environment variable to the browser executable.')

    # اكتب HTML في ملف مؤقت، واطبع PDF إلى ملف مؤقت أيضًا حتى لا يُستبدل الملف القديم إن فشلت الطباعة
    work_dir = tempfile.mkdtemp(prefix='gametranslator_tv_guide_')
    try:
        html_path = os.path.join(work_dir, 'guide.html')
        pdf_tmp = os.path.join(work_dir, 'guide.pdf')
        with open(html_path, 'w', encoding='utf-8') as f:
            f.write(html_content)

        # اطبع الصفحة إلى PDF بمتصفح بلا واجهة
        cmd = [browser, '--headless=new', '--disable-gpu', '--no-sandbox', '--no-pdf-header-footer',
               f'--print-to-pdf={pdf_tmp}', pathlib.Path(html_path).as_uri()]
        res = subprocess.run(cmd, capture_output=True, text=True)
        print('Chrome returncode:', res.returncode)
        if res.returncode != 0 or not os.path.isfile(pdf_tmp) or os.path.getsize(pdf_tmp) == 0:
            sys.exit('PDF generation failed.\n' + res.stderr)

        # انسخ الناتج إلى docs/ وإلى جذر المستودع
        os.makedirs(os.path.dirname(pdf_docs), exist_ok=True)
        shutil.copyfile(pdf_tmp, pdf_docs)
        shutil.copyfile(pdf_tmp, pdf_root)
    finally:
        shutil.rmtree(work_dir, ignore_errors=True)

    print('PDF generated successfully at:', pdf_docs)
    print('Copied to:', pdf_root)
    print('Size:', os.path.getsize(pdf_docs))


if __name__ == '__main__':
    main()
