// One-off utility to create a project-local Android debug keystore and wire it into
// PlayerSettings. After running once, this file can be deleted — the keystore is
// at Keys/quest-debug.keystore (gitignored) and the settings live in ProjectSettings.asset.
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class SetupQuestKeystore
{
    private const string KeystoreFileName = "quest-debug.keystore";
    private const string KeystoreAlias = "quest-debug";
    private const string KeystorePassword = "questdebug";
    private const string DName = "CN=Ali Jilani, OU=Dev, O=BlackWhaleStudio, L=Toronto, ST=Ontario, C=CA";

    [MenuItem("Tools/Lego/Set Up Quest Debug Keystore")]
    public static void Run()
    {
        var log = new System.Text.StringBuilder();
        try
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
            string keysFolder = projectRoot + "/Keys";
            string keystorePath = keysFolder + "/" + KeystoreFileName;
            string relativeKeystorePath = "Keys/" + KeystoreFileName;

            string jdkRoot = AndroidExternalToolsSettings.jdkRootPath;
            log.AppendLine("[SetupQuestKeystore] JDK root: " + jdkRoot);
            if (string.IsNullOrEmpty(jdkRoot))
                throw new Exception("AndroidExternalToolsSettings.jdkRootPath is empty. Open Preferences > External Tools and set the JDK.");

            string keytoolName = Application.platform == RuntimePlatform.WindowsEditor ? "keytool.exe" : "keytool";
            string keytoolPath = Path.Combine(jdkRoot, "bin", keytoolName);
            if (!File.Exists(keytoolPath))
                throw new Exception("keytool not found at: " + keytoolPath);
            log.AppendLine("[SetupQuestKeystore] keytool: " + keytoolPath);

            if (!Directory.Exists(keysFolder))
            {
                Directory.CreateDirectory(keysFolder);
                log.AppendLine("[SetupQuestKeystore] Created folder: " + keysFolder);
            }

            if (File.Exists(keystorePath))
            {
                log.AppendLine("[SetupQuestKeystore] Keystore already exists at " + keystorePath + " — keeping it; only updating PlayerSettings.");
            }
            else
            {
                var psi = new ProcessStartInfo
                {
                    FileName = keytoolPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = projectRoot,
                };
                psi.ArgumentList.Add("-genkeypair");
                psi.ArgumentList.Add("-noprompt");
                psi.ArgumentList.Add("-v");
                psi.ArgumentList.Add("-keystore"); psi.ArgumentList.Add(keystorePath);
                psi.ArgumentList.Add("-alias");    psi.ArgumentList.Add(KeystoreAlias);
                psi.ArgumentList.Add("-keyalg");   psi.ArgumentList.Add("RSA");
                psi.ArgumentList.Add("-keysize");  psi.ArgumentList.Add("2048");
                psi.ArgumentList.Add("-validity"); psi.ArgumentList.Add("18250");
                psi.ArgumentList.Add("-storepass"); psi.ArgumentList.Add(KeystorePassword);
                psi.ArgumentList.Add("-keypass");   psi.ArgumentList.Add(KeystorePassword);
                psi.ArgumentList.Add("-dname");     psi.ArgumentList.Add(DName);

                string stdout, stderr;
                int exitCode;
                using (var p = Process.Start(psi))
                {
                    stdout = p.StandardOutput.ReadToEnd();
                    stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    exitCode = p.ExitCode;
                }

                log.AppendLine("[SetupQuestKeystore] keytool exit=" + exitCode);
                if (!string.IsNullOrEmpty(stdout)) log.AppendLine("[SetupQuestKeystore] stdout: " + stdout.Trim());
                if (!string.IsNullOrEmpty(stderr)) log.AppendLine("[SetupQuestKeystore] stderr: " + stderr.Trim());

                if (exitCode != 0 || !File.Exists(keystorePath))
                    throw new Exception("keytool failed (exit=" + exitCode + "). See log.");

                log.AppendLine("[SetupQuestKeystore] Created keystore (" + new FileInfo(keystorePath).Length + " bytes): " + keystorePath);
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = relativeKeystorePath;
            PlayerSettings.Android.keystorePass = KeystorePassword;
            PlayerSettings.Android.keyaliasName = KeystoreAlias;
            PlayerSettings.Android.keyaliasPass = KeystorePassword;

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("File/Save Project");

            log.AppendLine("[SetupQuestKeystore] PlayerSettings.Android.useCustomKeystore = " + PlayerSettings.Android.useCustomKeystore);
            log.AppendLine("[SetupQuestKeystore] PlayerSettings.Android.keystoreName     = " + PlayerSettings.Android.keystoreName);
            log.AppendLine("[SetupQuestKeystore] PlayerSettings.Android.keyaliasName     = " + PlayerSettings.Android.keyaliasName);
            log.AppendLine("[SetupQuestKeystore] PlayerSettings.Android.keystorePass set = " + !string.IsNullOrEmpty(PlayerSettings.Android.keystorePass));
            log.AppendLine("[SetupQuestKeystore] PlayerSettings.Android.keyaliasPass set = " + !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass));
            log.AppendLine("[SetupQuestKeystore] DONE");
        }
        catch (Exception ex)
        {
            log.AppendLine("[SetupQuestKeystore] ERROR: " + ex.Message);
            log.AppendLine(ex.StackTrace ?? "");
        }
        finally
        {
            string logPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "SetupQuestKeystore.log");
            try { File.WriteAllText(logPath, log.ToString()); } catch { }
            Debug.Log(log.ToString());
        }
    }
}
