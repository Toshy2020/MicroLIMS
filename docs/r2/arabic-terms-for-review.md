# Arabic terms for review (F4, 2026-11-19)

The interface is now translated module by module. `frontend/src/locales/glossary.json` holds the approved terms and a test enforces them. While translating, the lanes needed many more terms. This list shows each one with the Arabic used, so you can confirm it or give the correct word. A term you correct is changed everywhere in one step.

## 1. Open questions

| # | English | Arabic used now | Question |
|---|---|---|---|
| Q1 | Assign approver (Document Control) | **Decided 2026-11-19: إسناد مُعتمِد** (إسناد for every "assign") | The rule for assigning work is إسناد (إسناد اختبار إلى محلل). For naming a person as approver, تعيين ("appoint") reads more naturally. Keep تعيين here, or use إسناد everywhere? |
| Q2 | Sample location / sampling point (EM, water) | **Decided 2026-11-19: موقع أخذ العينات** (the glossary check accepts العينات for "Sample") | The glossary test needs the exact word عينة wherever the English says "Sample", so "مواقع أخذ العينات" is not accepted. Keep موقع عينة, or relax the test for this phrase? |
| Q3 | Media lot | **Decided 2026-11-19: Lot = تشغيلة** (added to the glossary; Batch stays دفعة) | Batch (product) is دفعة (glossary). Media and reagent lots use تشغيلة. Confirm? |
| Q4 | Bench mode | **Decided 2026-11-19: وضع منصة العمل** (added to the glossary) | Confirm, or another word (وضع منصة العمل)? |
| Q5 | Retired (rule, specification) | مُوقَف | Confirm? |
| Q6 | Status names and enum values from the server | translated on chips; raw values in data tables | Confirm that tables may show the stored English value (e.g. room kind "Room/LAF")? |

## 2. Terms set once for all modules (please confirm)

| English | Arabic |
|---|---|
| Cryovial | قارورة مجمّدة |
| Recovery / Recovery % | الاسترداد / نسبة الاسترداد |
| Media lot | تشغيلة أوساط زرعية |
| GPT (growth promotion test) | اختبار تعزيز النمو (GPT) |
| Superseded | مُستبدَل |
| In service / Out of service | في الخدمة / خارج الخدمة |
| Manufacturer | مُصنِّع |
| Purity | نقاء |
| Qualification / Requalification | تأهيل / إعادة التأهيل |
| Maintenance | صيانة |
| Calibration | معايرة |
| Standardization (titrant) | تقييس |
| Assay | المقايسة |
| Analyte | المادة المحلَّلة |
| Theoretical plates | الصفائح النظرية |
| Mobile phase | الطور المتحرك |
| Elution | الشطف |
| Titrant | المعاير |
| Standard (reference material) | مادة قياسية |
| Label claim | الكمية المصرح بها |
| Replicate | تكرار |
| Related substances | المواد ذات الصلة |
| Residual solvent | مذيب متبقٍ |
| Disintegration / Disintegration time | التفكك / زمن التفكك |
| Dissolution | الذوبان |
| Weight variation | تفاوت الوزن |
| Tare | وزن الوعاء الفارغ |
| Specific absorbance | الامتصاص النوعي |
| Master entry | سجل رئيسي |
| Preparation | تحضير |
| Draft | مسودة |
| Effective (document) | ساري المفعول |
| Acknowledge | إقرار |
| Controlled document | مستند مضبوط |
| Release / Released | إفراج / مُفرَج عنه |
| Void / Voided | إبطال / مُبطل |
| Cancel / Cancelled | إلغاء / ملغى |

## 3. Other terms coined by area

- **Testing and samples:** تعيين (set, not assign) for "set as"; المرتجعات (returns); الإسناد الذاتي (self-assignment); الحضانة والمراقبة (incubation and observation); مُمْرِض (pathogen); مسحة / شطف (swab / rinse); ما بعد التنظيف (after cleaning).
- **Physicochemical:** معامل التذييل (tailing factor); معامل الاحتباس (retention factor); الاحتباس النسبي (relative retention); نسبة الإشارة إلى الضجيج (signal-to-noise); القمة إلى الوادي (peak-to-valley); شائبة / شوائب مسماة (impurity / named impurities); حد الإهمال (disregard limit); شوائب عنصرية (elemental impurities); الشكل الصيدلاني، قرص، كبسولة صلبة، كبسولة رخوة (dosage form, tablet, hard/soft capsule).
- **Microbiology media:** الكشف/التثبيط (indication/inhibition); خصائص الإغناء (enrichment characteristics); اللقاح الأولي (initial inoculum); عكر / رائق (turbid / clear); تشغيلة مخزون (stock lot); محلول محضَّر (prepared solution); التشغيلة المرجعية (reference lot); مزرعة العمل (working culture); سلالة مرجعية (reference strain).
- **Configuration:** بند مواصفات / معيار مواصفات (specification parameter); متدرج بالعد (count-tiered); غير منطبقة / عدم الانطباق (not applicable); الخصائص المصرح بها من المورد (supplier-stated attributes); تهيئة التحضير (preparation configuration); أماكن الاستخدام (where used); طريقة الاختبار القياسية (STM); إجراء تشغيل قياسي (SOP); الدرجة (cleanroom grade); حد التنبيه / حد الإجراء (alert / action limit); المحفّز (trigger); الدور المُخطَر (notified role); التخطيط (label layout); المصنف (workbook); إتلاف (destruction).
- **Inventory and equipment:** نفد من المخزون (out of stock); مُجمِّد عميق (deep freezer); ثلاجة (refrigerator); رطوبة (moisture); صيانة وقائية (preventive maintenance); إصلاح عطل (breakdown repair); إصدار البرنامج الثابت (firmware version); تتبع (traceability).
- **Reports and KPIs:** خريطة ضبط (control chart); اتجاه (trend); الملاءمة الطبيعية (normal fit); القيم الشاذة (outliers); الوسيط (median); اتجاه سلبي (adverse trend); معدل التنبيه / معدل الإجراء (alert / action rate); "ن" for "n =" in KPI tiles.
- **Document control:** طلب تغيير (change request); تدريب (training); ملغى الاستخدام (obsolete); إصدار (revision/version).
- **Administration and security:** صلاحية (permission); دور (role); المصادقة الثنائية (two-factor authentication); العامل الثاني للمصادقة (second factor); رمز الاسترداد (recovery code); مفتاح أمان مادي (hardware token); تطبيق المصادقة (authenticator app); جهاز موثوق (trusted computer); المهام الخلفية (background jobs); فصل المهام (segregation of duties); وكلاء الأجهزة (instrument agents); تدوير المفتاح (key rotation); الهيكل التنظيمي (organization); نقطة القطع (cut-off); المنفِّذ (actor); الموقِّع (signer).
- **Interface:** طرق العرض (views); كثافة مريحة / مضغوطة (comfortable / compact density); الجهاز اللوحي (tablet); تجاهل (discard); إخفاء (dismiss); لصق (paste); مناقشة، تعليق، مرفق، مُعدَّل (discussion, comment, attachment, edited); الجديد (what's new); معرّف الارتباط (correlation ID); حوادث (incidents); ملخص دوري (digest); تسليم (delivery); إلزامي (mandatory).

## 4. What stays English by design

- Printed and exported records (certificate of analysis, sample report, media and cryovial reports, equipment history, CSV and PDF exports), because the English record is the regulated one.
- The recorded meaning of every electronic signature. In Arabic, the screen shows the English statement first and the Arabic translation under it, with a note that the English text is recorded.
- Identifiers, codes, units, numbers, user names and data typed by users. These are shown left to right with Latin digits.
- Server messages that are not yet coded. The daily-work refusals are coded (895); configuration and administration refusals follow after R2.
