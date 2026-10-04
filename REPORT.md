# Отчёт сборки AuroraRP

- запуск: https://github.com/ItzHanchik/MarrowSDK-Extended/actions/runs/37225339864
- ref: refs/tags/aurorarp-v1.0.0 (fe33ffd4c4202755ff1b8d2fec494f6311b10c76)
- игровые сборки найдены: yes
- мод собран: yes

## Файлы сборки
```
total 780
drwxr-xr-x  2 runner runner   4096 Oct  4 18:40 .
drwxr-xr-x 13 runner runner   4096 Oct  4 18:41 ..
-rw-r--r--  1 runner runner 311981 Oct  4 18:40 AuroraRP-1.0.0-complete.zip
-rw-r--r--  1 runner runner 189421 Oct  4 18:40 AuroraRP-Thunderstore-1.0.0.zip
-rw-r--r--  1 runner runner   1560 Oct  4 18:40 AuroraRP-pallet-src-1.0.0.zip
-rw-r--r--  1 runner runner    394 Oct  4 18:40 AuroraRP.deps.json
-rw-r--r--  1 runner runner 247808 Oct  4 18:40 AuroraRP.dll
-rw-r--r--  1 runner runner    415 Oct  4 18:40 AuroraRPUpdater.deps.json
-rw-r--r--  1 runner runner  19968 Oct  4 18:40 AuroraRPUpdater.dll
ffc584d67fefe3fd6c9959fd2ab3b3bd9e5a592b962a68313d094eb6be45163f  out/AuroraRP-1.0.0-complete.zip
310ae30c1c825746bc670c84084ec1d4f4d9b6e6bc4f83f26f0f7c47268ffbcd  out/AuroraRP-Thunderstore-1.0.0.zip
03dfe007945e9d03a794cc1e921d88f78f2e5c45d2563af4625684440824b824  out/AuroraRP-pallet-src-1.0.0.zip
fac5788787573b576b6258034d9171ffc48327919895a9228ee937dd3bf2419a  out/AuroraRP.deps.json
3ccce7b10f7befb8fd7ad35c3e738ae7846aad06473f66cc245659ed13fbc47a  out/AuroraRP.dll
6a6835eade140daf779b94a1f0efd57b167bd0ecd415eaadc1d59da497e45670  out/AuroraRPUpdater.deps.json
73ecdc6da7bb7d08d94ae71ffd405361257f716d2e4572b3c5a327f23feb4285  out/AuroraRPUpdater.dll
```

## Игровые сборки
```
источник: черновик релиза в этом репозитории
архив: stage/assemblies_zip/Il2CppAssemblies.zip
DLL всего: 165
разложено в: stage/bonelab/BONELAB_Data/il2cpp/MelonLoader/Il2CppAssemblies (165 файлов)
первые файлы:
Assembly-CSharp.dll
Il2CppFacepunch.Steamworks.Win64.dll
Il2CppGoogle.Protobuf.dll
Il2CppGrpc.Core.dll
Il2CppHBAO.Demo.Runtime.dll
Il2CppHBAO.Demo.Universal.Runtime.dll
Il2CppHBAO.Runtime.dll
Il2CppHBAO.Universal.Runtime.dll
Il2CppMeshBakerCore.dll
Il2CppMicrosoft.Extensions.Configuration.Abstractions.dll
```

## Проверка AuroraRP.dll
```
размер: 247808 байт
тип:    PE32 executable (DLL) (console) Intel 80386 Mono/.Net assembly, for MS Windows, 3 sections
класс AuroraPack в сборке: 1
встроенный пак aurorarp.pack: 0
ссылки на игровые сборки: 9
```

## Окружение сборки
```
MelonLoader:
Dependencies
Documentation
net35
net6

MelonLoader/net6:
0Harmony.dll
AsmResolver.DotNet.dll
AsmResolver.PE.File.dll
AsmResolver.PE.dll
AsmResolver.dll
AssetRipper.VersionUtilities.dll
AssetsTools.NET.dll
Iced.dll
Il2CppInterop.Common.dll
Il2CppInterop.Generator.dll
Il2CppInterop.HarmonySupport.dll
Il2CppInterop.Runtime.dll
IndexRange.dll
MelonLoader.NativeHost.deps.json
MelonLoader.NativeHost.dll
MelonLoader.deps.json
MelonLoader.dll
MelonLoader.runtimeconfig.json
MelonLoader.xml
Microsoft.Bcl.AsyncInterfaces.dll
Microsoft.Diagnostics.NETCore.Client.dll
Microsoft.Diagnostics.Runtime.dll
Microsoft.Extensions.Configuration.Abstractions.dll
Microsoft.Extensions.Configuration.Binder.dll
Microsoft.Extensions.Configuration.dll
Microsoft.Extensions.DependencyInjection.Abstractions.dll
Microsoft.Extensions.Logging.Abstractions.dll
Microsoft.Extensions.Logging.dll
Microsoft.Extensions.Options.dll
Microsoft.Extensions.Primitives.dll
Microsoft.Win32.SystemEvents.dll
Mono.Cecil.Mdb.dll
Mono.Cecil.Pdb.dll
Mono.Cecil.Rocks.dll
Mono.Cecil.dll
MonoMod.Backports.dll
MonoMod.ILHelpers.dll
MonoMod.RuntimeDetour.dll
MonoMod.Utils.dll
MonoMod.dll
Newtonsoft.Json.dll
System.Configuration.ConfigurationManager.dll
System.Drawing.Common.dll
System.Security.Cryptography.ProtectedData.dll
System.Security.Permissions.dll
System.Windows.Extensions.dll
Tomlet.dll
UnityEngine.Il2CppAssetBundleManager.deps.json
UnityEngine.Il2CppAssetBundleManager.dll
UnityEngine.Il2CppImageConversionManager.deps.json
UnityEngine.Il2CppImageConversionManager.dll
WebSocketDotNet.dll
bHapticsLib.dll
runtimes

BoneLib в Mods: BoneLib.dll LabFusion.dll 

UnityEngine.XRModule.dll: stage/bonelab/BONELAB_Data/il2cpp/MelonLoader/Il2CppAssemblies/UnityEngine.XRModule.dll
Поиск типа InteractableHost в игровых сборках:
  Assembly-CSharp.dll: 
  Il2CppSLZ.Marrow.dll: SLZ.Marrow.InteractableHost 
```

## Лог сборки мода (последние 400 строк)
```
  Determining projects to restore...
  Restored /home/runner/work/MarrowSDK-Extended/MarrowSDK-Extended/AuroraRP/Mod~/AuroraRP.csproj (in 59 ms).
  AuroraRP: LabFusion найден — сборка с мультиплеером.
  AuroraRP: UnityEngine.XRModule найден — жест Y+A читается напрямую.
  AuroraRP -> /home/runner/work/MarrowSDK-Extended/MarrowSDK-Extended/out/AuroraRP.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.41
```
