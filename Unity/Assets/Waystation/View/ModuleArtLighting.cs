using System;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>Native lit module emissions follow operating state and the existing animated lamp drivers.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class ModuleArtLighting : MonoBehaviour
    {
        public enum Mode { Powered, AreaLamp, Running }
        [Serializable] public sealed class Surface
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color Emission;
            public Mode Control;
            public Transform Socket;
            [NonSerialized] public FxLight Lamp;
        }
        public Transform MovingSocket;
        public Surface[] Surfaces = new Surface[0];
        Building _building;
        Station _station;
        MaterialPropertyBlock _block;
        static readonly int EmissionId=Shader.PropertyToID("_EmissionColor");

        public void Bind(Building building,Station station,Transform owner)
        {
            _building=building;_station=station;
            var lamps=owner.GetComponentsInChildren<FxLight>(true);
            foreach(var surface in Surfaces)
            {
                surface.Lamp=null;
                if(surface.Socket==null)continue;
                float closest=.08f*.08f;
                foreach(var lamp in lamps)
                {
                    float distance=(lamp.transform.position-surface.Socket.position).sqrMagnitude;
                    if(distance<closest){closest=distance;surface.Lamp=lamp;}
                }
            }
            Refresh();
        }
        void LateUpdate()=>Refresh();
        void Refresh()
        {
            if(_building==null||_station==null)return;
            if(_block==null)_block=new MaterialPropertyBlock();
            bool powered=_building.Operating&&_station.SystemsOnline;
            foreach(var surface in Surfaces)
            {
                if(surface.Renderer==null)continue;
                float level=powered?Mathf.Clamp01(LightRig.PowerLevel):0f;
                if(surface.Control==Mode.Running&&!_building.Running)level=0f;
                if(powered&&surface.Lamp!=null)
                    level=surface.Lamp.Intensity>0f?surface.Lamp.Current/surface.Lamp.Intensity:0f;
                else if(surface.Control==Mode.AreaLamp)level*=1f-.75f*Mathf.Clamp01(LightRig.Daylight);
                if(surface.Control==Mode.Running&&!_building.Running)level=0f;
                surface.Renderer.GetPropertyBlock(_block,surface.MaterialIndex);
                _block.SetColor(EmissionId,surface.Emission*level);
                surface.Renderer.SetPropertyBlock(_block,surface.MaterialIndex);
                _block.Clear();
            }
        }
    }
}
