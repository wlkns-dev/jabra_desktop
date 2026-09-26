using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JabraDesktop.App.Views;

public partial class ConsentWindow : Window
{
    public bool Accepted { get; private set; }

    public ConsentWindow() => AvaloniaXamlLoader.Load(this);

    public ConsentWindow(LocalizationService localization, UiLanguage language) : this()
    {
        Title = localization[UiText.ConsentTitle];
        this.FindControl<TextBlock>("TitleText")!.Text = localization[UiText.ConsentTitle];
        this.FindControl<TextBlock>("SubtitleText")!.Text = localization[UiText.ConsentSubtitle];
        this.FindControl<TextBlock>("TermsText")!.Text = ConsentTerms.GetText(language);
        var decline = this.FindControl<Button>("DeclineButton")!;
        decline.Content = localization[UiText.DeclineAndExit];
        decline.Click += (_, _) => Close();
        var accept = this.FindControl<Button>("AcceptButton")!;
        accept.Content = localization[UiText.AgreeAndContinue];
        accept.Click += (_, _) =>
        {
            Accepted = true;
            Close();
        };
    }
}
