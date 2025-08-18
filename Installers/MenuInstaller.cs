using Zenject;
using OneClickLight.Menu;

namespace OneClickLight.Installers;

// This particular installer relates to bindings that are used in the main menu. It is related to the
// MainSettingsMenuViewControllersInstaller installer in the base game, and its InstallBindings is called when the
// game first loads into the main menu, and after settings are applied, which causes an internal reload of the game.

internal class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesTo<GameplayMenu>().AsSingle();
    }
}