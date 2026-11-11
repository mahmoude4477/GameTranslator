// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;

namespace GameTranslator.Core.LgTv;

/// <summary>إذا تعذّر الوصول إلى التلفزيون المحفوظ يبحث عنه في الشبكة من جديد: فلا يفسد الاقتران عندما يعطي المودم التلفزيون عنوانًا جديدًا.</summary>
public sealed class TvLocator(
    LgTv tv,
    Func<LgTv, Task<ITvControl>> connect,
    Func<Task<IReadOnlyList<LgTv>>> search,
    Action<LgTv> onMoved,
    TimeSpan? searchInterval = null)
{
    private readonly TimeSpan interval = searchInterval ?? TimeSpan.FromSeconds(30);
    private LgTv current = tv;
    private Stopwatch? sinceSearch;

    public async Task<ITvControl> ConnectAsync()
    {
        try
        {
            return await connect(current);
        }
        catch (Exception) when (SearchDue())
        {
            // يصل التنفيذ إلى هنا أيضًا والتلفزيون مطفأ؛ فلا تُمسح الشبكة في كل محاولة.
            var moved = Moved(current, await search());
            if (moved is null) throw;
            var control = await connect(moved);
            current = moved;
            onMoved(moved);
            return control;
        }
    }

    private bool SearchDue()
    {
        if (sinceSearch is not null && sinceSearch.Elapsed < interval) return false;
        sinceSearch = Stopwatch.StartNew();
        return true;
    }

    /// <summary>العنوان الجديد للتلفزيون المحفوظ بين ما عُثر عليه. إذا وُجد أكثر من تلفزيون غير معروف لا يخمّن.</summary>
    public static LgTv? Moved(LgTv saved, IReadOnlyList<LgTv> all)
    {
        var found = all.Where(tv => tv.Brand == saved.Brand).ToList();
        if (found.Any(tv => tv.Host == saved.Host)) return null;
        var sameName = found.Where(tv => tv.Name == saved.Name).ToList();
        if (sameName.Count == 1) return sameName[0];
        return found.Count == 1 ? found[0] : null;
    }
}
