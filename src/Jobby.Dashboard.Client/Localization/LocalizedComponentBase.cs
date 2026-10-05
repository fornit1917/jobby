using Microsoft.AspNetCore.Components;

namespace Jobby.Dashboard.Client.Localization;

public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject] protected LanguageState L { get; set; } = null!;

    protected override void OnInitialized() => L.Changed += OnLanguageChanged;

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => L.Changed -= OnLanguageChanged;
}
