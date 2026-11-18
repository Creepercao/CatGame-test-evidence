using System;
using System.Collections;
using System.Collections.Generic;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.UI;

namespace CatGame
{
    /// <summary>Swap only room art/footprints; retain actors, economy, HUD, items and save schema.</summary>
    public sealed class RoomThemeController : MonoBehaviour
    {
        public RoomThemeSettings Settings { get; private set; }
        public string CurrentId { get { return _index<0?null:Settings.Themes[_index].Id; } }
        public bool IsTransitioning { get; private set; }
        public int ChangeCount { get; private set; }
        CatWorld _world;
        GameState _state;
        int _index=-1;
        GameObject _furniture, _veil;
        ModalGate _ownedGate;
        Coroutine _transition;
        string _failedId;

        public void Initialize(CatWorld world, GameState state, RoomThemeSettings settings=null)
        {
            _world=world;_state=state;
            Settings=settings!=null?settings:Resources.Load<RoomThemeSettings>(RoomThemeSettings.ResourcePath);
            if(Settings==null) throw new InvalidOperationException("Missing room theme settings");
            Settings.Validate();
            Apply(Settings.Resolve(state.totalEarned));
        }

        void LateUpdate()
        {
            if(_world==null || _state==null || IsTransitioning || _world.Hud==null)return;
            int next=Settings.Resolve(_state.totalEarned);
            if(next<=_index || Settings.Themes[next].Id==_failedId)return;
            var gate=_world.Hud.Modals;
            // Give already pending story, intro, settings and same-frame input suppression priority.
            if(gate==null || gate.InputBlocked || !_state.introCompleted ||
               (_world.EnableStoryEvents && (_state.pendingStoryEvents.Count>0 ||
                 (_state.rocketPurchased && !_state.endingCompleted))))return;
            if(!gate.TryAcquire(this))return;
            _ownedGate=gate;
            _transition=StartCoroutine(Transition(next));
        }

        IEnumerator Transition(int next)
        {
            IsTransitioning=true;
            _veil=new GameObject("RoomTransition",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas=_veil.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
            var image=UiKit.Panel(_veil.transform,"Fade",new Color(.99f,.96f,.92f,0),false);
            image.rectTransform.Stretch();
            float half=Settings.TransitionSeconds*.5f;
            for(float t=0;t<half;t+=Time.unscaledDeltaTime)
            {image.color=new Color(.99f,.96f,.92f,t/half);yield return null;}
            image.color=new Color(.99f,.96f,.92f,1);
            try { Apply(next); }
            catch(Exception error)
            {
                _failedId=Settings.Themes[next].Id;
                Debug.LogError("Room theme could not be applied: "+error.Message);
            }
            for(float t=0;t<half;t+=Time.unscaledDeltaTime)
            {image.color=new Color(.99f,.96f,.92f,1-t/half);yield return null;}
            ReleaseTransition();
        }

        void Apply(int index)
        {
            var surface=_world.WalkSurface;
            if(surface==null || surface.Background==null)throw new InvalidOperationException("Room theme requires a room surface");
            var definition=Settings.Themes[index];
            var text=Resources.Load<TextAsset>(definition.ResourceRoot+"/layout");
            var background=Resources.Load<Sprite>(definition.ResourceRoot+"/architecture");
            if(text==null || background==null)throw new InvalidOperationException("Missing room theme art: "+definition.Id);
            var layout=JsonUtility.FromJson<RoomThemeLayout>(text.text);
            ValidateLayout(layout);
            if(background.rect.size!=new Vector2(layout.width,layout.height) ||
                background.pixelsPerUnit!=WorldArtSettings.Current.WorldPixelsPerUnit)
                throw new InvalidOperationException("Theme architecture does not match world grid");
            // Resolve every asset before changing any live room state.
            var sprites=new Sprite[layout.placements.Length];
            for(int i=0;i<sprites.Length;i++)
            {
                sprites[i]=Resources.Load<Sprite>(definition.ResourceRoot+"/"+layout.placements[i].asset);
                if(sprites[i]==null || sprites[i].rect.size!=new Vector2(layout.cell,layout.cell) ||
                   sprites[i].pixelsPerUnit!=background.pixelsPerUnit)
                    throw new InvalidOperationException("Missing or invalid room prop "+layout.placements[i].asset);
            }
            var parent=new GameObject("BackWallFurniture");parent.SetActive(false);
            parent.transform.SetParent(surface.Background.transform,false);
            var groups=new Dictionary<string,Transform>();
            var pieces=new Dictionary<string,List<SpriteRenderer>>();
            float ppu=background.pixelsPerUnit;
            for(int i=0;i<sprites.Length;i++)
            {
                var placement=layout.placements[i];
                Transform group;
                if(!groups.TryGetValue(placement.group,out group))
                {
                    group=new GameObject(placement.group).transform;group.SetParent(parent.transform,false);
                    groups.Add(placement.group,group);pieces.Add(placement.group,new List<SpriteRenderer>());
                    if(!placement.wallDecor)
                    {
                        var depth=group.gameObject.AddComponent<RoomFurnitureDepth>();
                        depth.LocalGround=new Vector3((placement.groundX-layout.width*.5f)/ppu,
                            (layout.height*.5f-placement.groundY)/ppu,0);
                    }
                }
                var piece=new GameObject(placement.asset);piece.transform.SetParent(group,false);
                piece.transform.localPosition=new Vector3((placement.x+layout.cell*.5f-layout.width*.5f)/ppu,
                    (layout.height*.5f-placement.y-layout.cell*.5f)/ppu,0);
                var renderer=piece.AddComponent<SpriteRenderer>();renderer.sprite=sprites[i];renderer.sortingOrder=-50;
                pieces[placement.group].Add(renderer);
            }
            foreach(var group in groups)
            {
                var depth=group.Value.GetComponent<RoomFurnitureDepth>();
                if(depth!=null)depth.Pieces=pieces[group.Key].ToArray();
            }
            var old=_furniture!=null?_furniture:surface.Background.transform.Find("BackWallFurniture")?.gameObject;
            if(old==parent)old=null;
            if(old!=null){old.SetActive(false);old.name="RetiredRoomFurniture";Destroy(old);}
            surface.Background.sprite=background;
            var obstacles=new Rect[layout.obstacles.Length];
            for(int i=0;i<obstacles.Length;i++)obstacles[i]=layout.obstacles[i].ToRect();
            surface.Obstacles=obstacles;
            parent.SetActive(true);_furniture=parent;_index=index;ChangeCount++;
            if(_world.Roster!=null)_world.Roster.RevalidateRoom();
        }

        public static void ValidateLayout(RoomThemeLayout layout)
        {
            if(layout==null || layout.width!=512 || layout.height!=288 || layout.cell!=64 ||
               layout.cornerX!=256 || layout.cornerY!=40 || Mathf.Abs(layout.wallSlope-.36f)>.0001f ||
               layout.placements==null || layout.obstacles==null)
                throw new InvalidOperationException("Theme floor geometry must match the authored room");
            foreach(var p in layout.placements)
                if(p==null || string.IsNullOrEmpty(p.asset) || string.IsNullOrEmpty(p.group))
                    throw new InvalidOperationException("Invalid theme placement");
            foreach(var r in layout.obstacles)
                if(r==null || r.width<=0 || r.height<=0 || r.x<0 || r.y<0 ||
                   r.x+r.width>512 || r.y+r.height>288 || r.ToRect().Contains(new Vector2(315,151)))
                    throw new InvalidOperationException("Invalid furniture footprint or blocked home centre");
        }

        void ReleaseTransition()
        {
            if(_veil!=null)Destroy(_veil);_veil=null;
            if(_ownedGate!=null){_ownedGate.SuppressGameplayThisFrame();_ownedGate.Release(this);}
            _ownedGate=null;IsTransitioning=false;_transition=null;
        }
        void OnDisable()
        {
            if(_transition!=null)StopCoroutine(_transition);
            ReleaseTransition();
        }
    }
}
