/* SPDX-License-Identifier: AGPL-3.0-only */
/* واجهة C بسيطة فوق libchiaki لتطبيق GameTranslator على Windows (gametranslator_native.dll).
 * جانب C# يستدعي دوال هذا الملف الرأسي فقط عبر P/Invoke؛ ولا يمس بنى libchiaki مباشرة.
 * كل السلاسل النصية UTF-8 وكل الدوال آمنة مع تعدد الخيوط. قيمة الإرجاع 0 = نجاح، وغيرها خطأ. */
#ifndef GAMETRANSLATOR_NATIVE_H
#define GAMETRANSLATOR_NATIVE_H

#include <stdint.h>

#ifdef _WIN32
#define CS_API __declspec(dllexport)
#else
#define CS_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* سطر من سجل libchiaki. level: 0 خطأ، 1 تحذير، 2 معلومة. */
typedef void (*cs_log_cb)(int level, const char *message, void *user);

/* يجهّز مكدس الشبكة (WSAStartup). يُستدعى مرة واحدة عند بدء التطبيق. */
CS_API int cs_init(void);

/* --- البحث عن PS5 وإيقاظه --- */

/* يُستدعى لكل PS5 يُعثر عليه. awake: 1 يعمل، 0 في وضع الراحة. */
typedef void (*cs_console_cb)(const char *host, const char *name, int awake, void *user);

/* targets: عناوين IPv4 التي تُسأل (مثل "192.168.1.20", "255.255.255.255"). يكرر البحث كل 500 ms
 * طوال timeout_ms؛ وقد يُبلَّغ عن PS5 نفسه أكثر من مرة. حاجب. */
CS_API int cs_discover(const char *const *targets, int target_count, int timeout_ms, cs_console_cb callback, void *user);

/* يوقظ PS5 المسجَّل من وضع الراحة. credential: القيمة الست عشرية لمفتاح التسجيل. */
CS_API int cs_wake(const char *host, uint64_t credential);

/* --- التسجيل (رمز «ربط الجهاز» في PS5) --- */

typedef struct {
    char nickname[0x20];      /* اسم PS5 منتهيًا بصفر */
    uint8_t regist_key[0x10]; /* ست عشري بترميز ASCII، مكمَّل بالأصفار */
    uint8_t morning[0x10];
} cs_registered_console;

/* حاجب (~30 ثانية على الأكثر). عند الفشل يحمل *reason السبب الذي أبلغ به PS5
 * (مثل 0x80108b02 = لم يُتعرَّف على الحساب)؛ و 0 إن كان مجهولًا. الإرجاع: 0 نجاح، 1 فشل، 2 إلغاء، <0 تعذر البدء. */
CS_API int cs_register(const char *host, const uint8_t account_id[8], uint32_t pin,
                       cs_registered_console *out, uint32_t *reason, cs_log_cb log, void *user);

/* --- بث الصورة --- */

/* إطار مفكوك الترميز: BGRA، وفي كل سطر stride بايتًا. الذاكرة صالحة طوال رد النداء فقط. */
typedef void (*cs_frame_cb)(const uint8_t *bgra, int width, int height, int stride, void *user);

/* أحداث الجلسة. */
enum {
    CS_EVENT_CONNECTED = 0,
    CS_EVENT_QUIT = 1,            /* quit_reason: إحدى قيم CS_QUIT_* أدناه */
    CS_EVENT_LOGIN_PIN = 2        /* للمستخدم الثاني رمز دخول على PS5 */
};
enum {
    CS_QUIT_STOPPED = 0,
    CS_QUIT_IN_USE = 1,           /* Remote Play مستخدم على جهاز آخر */
    CS_QUIT_REFUSED = 2,          /* رُفض الاتصال / التسجيل غير صالح */
    CS_QUIT_NETWORK = 3,          /* انقطع الاتصال */
    CS_QUIT_OTHER = 4             /* يشرحه reason_text */
};
typedef void (*cs_event_cb)(int event, int quit_reason, const char *reason_text, void *user);

typedef struct cs_session cs_session;

/* يفتح بث H.264 بدقة 1080p/30 والصوت ومدخلات ذراع التحكم مغلقة. يُفك ترميز كل إطار؛ ويُحوَّل إلى BGRA
 * ويُسلَّم إلى رد النداء frame بما لا يتجاوز max_fps مرة في الثانية. عند الخطأ NULL. ردود النداء تصل في خيط
 * الجلسة نفسها. */
CS_API cs_session *cs_session_start(const char *host, const uint8_t regist_key[0x10], const uint8_t morning[0x10],
                                    const uint8_t account_id[8], int max_fps,
                                    cs_frame_cb frame, cs_event_cb event, cs_log_cb log, void *user);

/* يوقف الجلسة وينتظر خيوطها ويحرر الذاكرة. يجب ألا يُستدعى من داخل رد نداء. */
CS_API void cs_session_stop(cs_session *session);

#ifdef __cplusplus
}
#endif

#endif
