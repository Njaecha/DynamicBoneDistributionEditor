using ADV.Commands.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Illusion.Extensions;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicBoneDistributionEditor
{
    internal class DBDEGizmoController : MonoBehaviour
    {
        public DBDEDynamicBoneEdit Editing;

        private Material _gizmoMaterial;

        internal static DBDEGizmoController Instance; 

        void Awake( )
        {
            // Unity has a built-in shader that is useful for drawing simple colored things.
            var shader = Shader.Find("Hidden/Internal-Colored");
            _gizmoMaterial = new Material(shader);
            _gizmoMaterial.hideFlags = HideFlags.HideAndDontSave;

            // Turn on alpha blending
            _gizmoMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _gizmoMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            // Always draw
            _gizmoMaterial.SetInt("_Cull", (int)CullMode.Off);
            _gizmoMaterial.SetInt("_ZWrite", 0);
            _gizmoMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            
            Instance = this;
        }

        void OnPostRender()
        {
            if (!DBDE.drawGizmos.Value) return;
            if (Editing == null || Editing.PrimaryDynamicBone == null || Editing.PrimaryDynamicBone?.m_Root == null) return;
            _gizmoMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);

            List<Transform> leafs = TraverseBone(Editing.PrimaryDynamicBone);

            foreach (Transform leaf in leafs)
            {
                Vector3 gTip = DrawArrow(leaf.position, Editing.gravity.value, Color.blue, 40);
                Vector3 fTip = DrawArrow(gTip, Editing.force.value, Color.red, 40);
                DrawArrow(leaf.position, fTip - leaf.position, Color.green, 1);
            }

            GL.PopMatrix();
        }

        private static List<Transform> TraverseBone(DynamicBone db)
        {
            List<Transform> particles = db.m_Particles.Select(p => p.m_Transform).ToList();
            var thingies = new List<Transform> { db.m_Root.transform };
            thingies.AddRange(particles);
            thingies.AddRange(db.m_notRolls);
            thingies.AddRange(db.m_Exclusions);
            
            List<Transform> leafs = new List<Transform>();
            foreach (Transform transform in thingies)
            {
                if (!transform || !transform.parent) continue;
                Color color = particles.Contains(transform.parent) ? Color.magenta : Color.gray;
                //DrawPyramid(color, transform.position, transform.parent.position, 0.1f);
                //DrawPyramid(color, transform);
                DrawBone(transform, color);

                if (thingies.Any(t => transform.Children().Contains(t))) continue;
                //DrawPyramid(new Color(0.4f, 0.1f, 0.5f), transform.position + (transform.forward.normalized * 0.01f), transform.position, 0.1f);
                //DrawPyramid(new Color(0.4f, 0.1f, 0.5f), transform.position + (-transform.forward.normalized * 0.01f), transform.position, 0.1f);
                color = particles.Contains(transform) ? new Color(0.4f, 0.1f, 0.5f) : Color.gray;
                DrawTransformCube(transform, color);
                leafs.Add(transform);
            }
            return leafs;
        }

        private static Vector3 DrawArrow(Vector3 Base, Vector3 relativeTip, Color color, int factor)
        {
            GL.Begin (GL.LINES);
            GL.Color(color);
            GL.Vertex(Base);
            Vector3 tip = Base + relativeTip * factor;
            GL.Vertex(tip);
            GL.End ();
            DrawPyramid(color, tip, Base + relativeTip * (factor * 0.8f), 0.05f);
            return tip;
        }


        private static void DrawPyramid(Color color, Transform transform, float radius = 0.1f)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            List<Vector3> cornerPoints = CalculateBasePoints(transform.position, transform.parent.position, radius, transform.parent.position + transform.parent.right);
            GL.Vertex(transform.position);
            GL.Vertex(cornerPoints[0]);
            GL.Vertex(cornerPoints[1]);
            GL.Vertex(transform.position);
            GL.Vertex(cornerPoints[1]);
            GL.Vertex(cornerPoints[2]); 
            GL.Vertex(transform.position);
            GL.Vertex(cornerPoints[2]);
            GL.Vertex(cornerPoints[3]);
            GL.Vertex(transform.position);
            GL.Vertex(cornerPoints[3]);
            GL.Vertex(cornerPoints[0]);
            GL.End();
        }
        
        private static void DrawPyramid( Color color, Vector3 tip, Vector3 baseCenter, float radius = 0.35f)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            List<Vector3> cornerpoints = CalculateBasePoints(tip, baseCenter, radius);
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[0]);
            GL.Vertex(cornerpoints[1]);
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[1]);
            GL.Vertex(cornerpoints[2]); 
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[2]);
            GL.Vertex(cornerpoints[3]);
            GL.Vertex(tip);
            GL.Vertex(cornerpoints[3]);
            GL.Vertex(cornerpoints[0]);
            GL.End();
        }

        // AI generated :)
        private static List<Vector3> CalculateBasePoints(Vector3 tip, Vector3 baseCenter, float radius, Vector3? cornerPoint = null)
        {
            Vector3 c = tip - baseCenter;
            Vector3 pA;
            if (cornerPoint.HasValue)
            {
                pA = ((cornerPoint.Value - baseCenter) * (radius/10));
            }
            else
            {
                Vector3 v = Vector3.Cross(Vector3.forward, c);
                if (v == Vector3.zero) v = Vector3.Cross(Vector3.up, c);
                pA = v.normalized*(radius/10);
            }
            Vector3 pB = Quaternion.AngleAxis(90, c) * pA;
            Vector3 pC = Quaternion.AngleAxis(180, c) * pA;
            Vector3 pD = Quaternion.AngleAxis(270, c) * pA;

            return new List<Vector3>() { baseCenter+pA, baseCenter+pB, baseCenter + pC, baseCenter + pD };
        }

        private static void DrawBone(Transform tip, Color? color = null)
        {
            if (!color.HasValue) color = Color.magenta;

            Transform t = tip;
            Transform p = tip.parent;
            Vector3 h = p.position + (t.position - p.position) * 0.1f;

            List<KeyValuePair<float, Vector3>> assCrack = new List<KeyValuePair<float, Vector3>>
            {
                new KeyValuePair<float, Vector3>(Vector3.Cross(t.position - p.position, p.up).sqrMagnitude, p.up),
                new KeyValuePair<float, Vector3>(Vector3.Cross(t.position - p.position, p.right).sqrMagnitude, p.right),
                new KeyValuePair<float, Vector3>(Vector3.Cross(t.position - p.position, p.forward).sqrMagnitude, p.forward)
            };
            
            assCrack.Sort((kvp1 , kvp2) => kvp1.Key.CompareTo(kvp2.Key));
            Vector3 a = assCrack[2].Value;
            Vector3 b = assCrack[1].Value;
            

            GL.Begin(GL.TRIANGLES);
            GL.Color(color.Value);
            
            GL.Vertex(t.position);
            GL.Vertex(h + a * 0.01f);
            GL.Vertex(h + b * 0.01f);
            GL.Vertex(t.position);
            GL.Vertex(h + a * -0.01f);
            GL.Vertex(h + b * 0.01f);
            GL.Vertex(t.position);
            GL.Vertex(h + a * 0.01f);
            GL.Vertex(h + b * -0.01f);
            GL.Vertex(t.position);
            GL.Vertex(h + a * -0.01f);
            GL.Vertex(h + b * -0.01f);
            
            GL.Vertex(p.position);
            GL.Vertex(h + a * 0.01f);
            GL.Vertex(h + b * 0.01f);
            GL.Vertex(p.position);
            GL.Vertex(h + a * -0.01f);
            GL.Vertex(h + b * 0.01f);
            GL.Vertex(p.position);
            GL.Vertex(h + a * 0.01f);
            GL.Vertex(h + b * -0.01f);
            GL.Vertex(p.position);
            GL.Vertex(h + a * -0.01f);
            GL.Vertex(h + b * -0.01f);
            
            GL.End();
            
            
        }

        private static void DrawTransformCube(Transform t, Color? color = null)
        {
            if (!color.HasValue) color = new Color(0.4f, 0.1f, 0.5f);
            Vector3 p = t.position;
            GL.Begin(GL.TRIANGLES);
            GL.Color(color.Value);
            
            GL.Vertex(p+ t.forward * 0.01f);
            GL.Vertex(p + t.right * 0.01f);
            GL.Vertex(p + t.up * 0.01f);
            GL.Vertex(p+ t.forward * 0.01f);
            GL.Vertex(p + t.right * 0.01f);
            GL.Vertex(p + t.up * -0.01f);
            GL.Vertex(p+ t.forward * 0.01f);
            GL.Vertex(p + t.right * -0.01f);
            GL.Vertex(p + t.up * -0.01f);
            GL.Vertex(p+ t.forward * 0.01f);
            GL.Vertex(p + t.right * -0.01f);
            GL.Vertex(p + t.up * 0.01f);
            
            GL.Vertex(p+ t.forward * -0.01f);
            GL.Vertex(p + t.right * 0.01f);
            GL.Vertex(p + t.up * 0.01f);
            GL.Vertex(p+ t.forward *- 0.01f);
            GL.Vertex(p + t.right * 0.01f);
            GL.Vertex(p + t.up * -0.01f);
            GL.Vertex(p+ t.forward * -0.01f);
            GL.Vertex(p + t.right * -0.01f);
            GL.Vertex(p + t.up * -0.01f);
            GL.Vertex(p+ t.forward * -0.01f);
            GL.Vertex(p + t.right * -0.01f);
            GL.Vertex(p + t.up * 0.01f);
            
            GL.End();
        }
    }
}
