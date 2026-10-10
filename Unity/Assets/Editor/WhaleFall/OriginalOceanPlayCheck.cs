using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WhaleFallEditor
{
    [InitializeOnLoad]
    public static class OriginalOceanPlayCheck
    {
        const string Flag="OriginalOcean.PlayCheck";
        static readonly Vector3[] Targets={new Vector3(0,2,26),new Vector3(0,-6,36),new Vector3(0,-15,46),new Vector3(0,-24,58),new Vector3(0,-27,66)};
        static int phase;
        static double start;
        static StringBuilder report;
        static bool started;
        static OriginalOceanPlayCheck() { EditorApplication.update+=Tick; }
        public static void Begin()
        {
            if(EditorSceneManager.GetActiveScene().path!=OriginalOceanSceneBuilder.Destination)
                throw new InvalidOperationException("Open the original-scene adaptation first.");
            if(EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the new scene before validation.");
            SessionState.SetBool(Flag,true); EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false) || !EditorApplication.isPlaying || !Application.isPlaying || EditorApplication.isPaused) return;
            var diver=UnityEngine.Object.FindObjectOfType<WhaleFall.OriginalOceanDiver>();
            if(!diver) return;
            if(!started)
            {
                started=true; start=EditorApplication.timeSinceStartup; phase=0; report=new StringBuilder("PLAY-MODE VALIDATION\n");
                Application.runInBackground=true;
                Application.logMessageReceived+=OnLog;
                diver.testDrive=true;
            }
            if(Mathf.Abs(diver.transform.position.x-diver.spawn.x)>.01f) { Finish("FAIL: player left 2D plane"); return; }
            if(phase<Targets.Length)
            {
                var delta=Targets[phase]-diver.transform.position;
                diver.testInput=new Vector2(delta.z,delta.y).normalized;
                if(delta.magnitude<.9f)
                {
                    report.AppendLine("PASS waypoint "+phase+": "+diver.transform.position);
                    if(phase==1 || phase==4) OriginalOceanSceneBuilder.Capture(diver.followCamera,"playable-depth-"+phase);
                    phase++; start=EditorApplication.timeSinceStartup;
                }
                else if(EditorApplication.timeSinceStartup-start>15)
                {
                    var rb=diver.GetComponent<Rigidbody>();
                    report.AppendLine("Diagnostics: time="+Time.time+" scale="+Time.timeScale+" frame="+Time.frameCount+" velocity="+rb.velocity+" constraints="+rb.constraints+" kinematic="+rb.isKinematic+" test="+diver.testDrive+" enabled="+diver.enabled+" active="+diver.gameObject.activeInHierarchy);
                    Finish("FAIL: path blocked at "+diver.transform.position+" target="+Targets[phase]);
                }
            }
            else
            {
                diver.testInput=Vector2.down;
                if(EditorApplication.timeSinceStartup-start>5)
                {
                    if(diver.GetComponent<Rigidbody>().velocity.y<-.1f) Finish("FAIL: floor did not stop descent");
                    else
                    {
                        report.AppendLine("PASS floor collision at "+diver.transform.position);
                        report.AppendLine("PASS fixed X plane and camera follow");
                        diver.ResetPosition();
                        Finish("COMPLETE");
                    }
                }
            }
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception) report?.AppendLine("RUNTIME ERROR: "+message);
        }
        static void Finish(string result)
        {
            report.AppendLine(result);
            File.WriteAllText(OriginalOceanSceneBuilder.Reports+"/play-check.txt",report.ToString());
            Application.logMessageReceived-=OnLog;
            SessionState.SetBool(Flag,false); started=false;
            EditorApplication.isPlaying=false;
        }
    }
}
