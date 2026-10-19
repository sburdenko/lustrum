using CommunityToolkit.Mvvm.Input;

namespace Lustrum.App.ViewModels;

/// <summary>One entry of the card's Copy to menu; a refused target stays listed with its reason.</summary>
public sealed record CopyTarget(string Label, IAsyncRelayCommand Copy);
