// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Shell;

/// <summary>
/// Windows Güvenlik Duvarı'nda GameTranslator'a gelen bağlantı izni. Remote Play (PS5) ve altyazı sunucusu (TV) dışarıdan
/// bağlantı alır; kullanıcı ilk açılıştaki izin sorusunu kapattıysa ya da ağ "Ortak" ise bu bağlantılar engellenir.
/// </summary>
public interface IFirewall
{
    /// <summary>Bu exe için her profilde (Etki alanı, Özel, Ortak) TCP ve UDP'ye açık, engellenmemiş izin kuralı var mı. Yönetici gerekmez.</summary>
    bool HasRule();

    /// <summary>Yönetici izniyle (UAC) kuralı ekler; kullanıcı izin vermezse ya da kural yine yoksa false.</summary>
    Task<bool> AddRuleAsync();
}

/// <summary>
/// Güvenlik duvarı kartının durumu. Offer: kural eksik ve bağlantı sorunlu, düğme görünür. Working: Windows'un izin
/// sorusu bekleniyor. Allowed: kural eklendi (kısa onay). Failed: kural eklenemedi, düğme yeniden görünür.
/// </summary>
public enum FirewallPrompt { None, Offer, Working, Allowed, Failed }
