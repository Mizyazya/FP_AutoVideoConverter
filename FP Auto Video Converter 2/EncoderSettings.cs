namespace FP_Auto_Video_Converter_2
{
    enum EncoderKind { Cpu, Nvenc, Qsv }

    // Кожен варіант кодування налаштовується тут окремо і самодостатньо — щоб додати
    // новий (напр. AMD AMF) пізніше, досить додати один case у кожен метод нижче плюс
    // одну радіокнопку й одну пробу визначення заліза в Form1, більше нічого чіпати не треба.
    static class EncoderSettings
    {
        public static string GetHwaccelPrefix(EncoderKind kind)
        {
            switch (kind)
            {
                case EncoderKind.Nvenc: return "-hwaccel cuda ";
                default: return ""; // QSV: тестувався без -hwaccel qsv — воно апаратно не вміє декодувати ProRes/4:2:2, додавати без окремої перевірки ризиковано
            }
        }

        public static string BuildVideoCodecArgs(EncoderKind kind, int crf, string cpuPreset, string nvencPreset)
        {
            switch (kind)
            {
                case EncoderKind.Nvenc:
                    // Калібровано на 4 різних типах контенту (шумне відео, чистий ProRes-майстер,
                    // анімація, динамічна сцена). Будь-який фіксований офсет -cq (+1..+4), що рятував
                    // розмір на одному контенті, провалював якість на іншому (найгірше: +4 → VMAF
                    // впало з 97 до 85 на чистому ProRes). НЕ додавати офсет без повторного калібрування.
                    return $"-c:v hevc_nvenc -preset {nvencPreset} -tune hq -rc vbr -cq {crf} -b:v 0";
                case EncoderKind.Qsv:
                    // Калібрований лише на 2 з 4 типів контенту (там -global_quality≈CRF-4 підходив).
                    // З огляду на урок з NVENC (офсет з малої вибірки виявився шкідливим) — свідомо
                    // консервативно: CRF напряму, без офсету. Без -preset: тестувався лише з -global_quality.
                    return $"-c:v hevc_qsv -global_quality {crf}";
                default:
                    return $"-c:v libx265 -preset {cpuPreset} -crf {crf}";
            }
        }

        // hevc_nvenc падає (0 байт, "Failed setup for format cuda") на 4:2:2-джерелах (напр. ProRes).
        // QSV такого бага не має (перевірено).
        public static bool NeedsPixelFormatFix(EncoderKind kind) => kind == EncoderKind.Nvenc;

        // В той самий -vf, що й scale (через кому) — ніколи окремим -pix_fmt (знищив би 10-біт HDR).
        public const string PixelFormatFixFilter = "format=yuv420p|p010le";

        // Пробне кодування 1 кадру — найпростіший універсальний спосіб перевірити, чи реально
        // працює апаратний кодер на цій машині (а не просто "чи є відеокарта в списку"), без
        // прав адміністратора і без нових залежностей (WMI тощо).
        public static string GetProbeArguments(EncoderKind kind)
        {
            string codec = kind == EncoderKind.Nvenc ? "hevc_nvenc" : "hevc_qsv";
            // 64x64 виявилось замалим - і NVENC, і QSV відмовляють у кодуванні нижче свого
            // мінімального розміру ("Current resolution is unsupported"). 320x240 - безпечний мінімум.
            return $"-hide_banner -loglevel error -f lavfi -i testsrc=duration=1:size=320x240:rate=1 -c:v {codec} -f null -";
        }
    }
}
