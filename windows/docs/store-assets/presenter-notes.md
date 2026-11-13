# A WinUI app, live on stage

## Build the app

```powershell
dotnet build .\DemoApp.csproj -p:Platform=x64
```

## Add a Fluent action

```xml
<Button
	Content="Try it"
	Style="{StaticResource AccentButtonStyle}"
	Click="OnTryItClick" />
```

## Show inline feedback

```xml
<InfoBar x:Name="StatusBar"
	Title="Ready for your next idea"
	Severity="Success" IsOpen="False" />
```

## Handle the action

```csharp
private void OnTryItClick(object sender, RoutedEventArgs e)
{
	StatusBar.IsOpen = true;
}
```

## Ask your coding assistant

> Add a keyboard accelerator to this WinUI 3 button.
> Use Ctrl+Shift+R and explain accessibility.

## Build for the next step

```powershell
dotnet build .\DemoApp.csproj -c Release -p:Platform=x64
```