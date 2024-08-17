using static Microsoft.Maui.ApplicationModel.Permissions;

namespace GameX.App.Explorer
{
//    public class ReadWriteStoragePerms : BasePlatformPermission
//    {
//#if __ANDROID__
//        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => new List<(string androidPermission, bool isRuntime)>
//        {
//            (global::Android.Manifest.Permission.ReadExternalStorage, true),
//            (global::Android.Manifest.Permission.WriteExternalStorage, true)
//        }.ToArray();
//#endif
//    }

    public partial class App
    {
        static bool HasPermissions()
        {
            var status = CheckAndRequestPermission<StorageWrite>().Result;
            if (status == PermissionStatus.Granted)
                status = CheckAndRequestPermission<StorageRead>().Result;
            if (status != PermissionStatus.Granted)
            {
                Current.MainPage.DisplayAlert("Prompt", $"NO ACCESS", "Cancel").Wait();
                return false;
            }
            return true;
        }

        async static Task<PermissionStatus> CheckAndRequestPermission<TPermission>() where TPermission : BasePermission, new()
        {
            var status = await CheckStatusAsync<TPermission>();
            if (status == PermissionStatus.Granted) return status;
            else if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                await Current.MainPage.DisplayAlert("Prompt", $"turn on in settings", "Cancel");
                return status;
            }
            else if (ShouldShowRationale<TPermission>())
            {
                await Current.MainPage.DisplayAlert("Prompt", "Why the permission is needed", "Cancel");
            }
            status = await RequestAsync<TPermission>();
            return status;
        }
    }
}