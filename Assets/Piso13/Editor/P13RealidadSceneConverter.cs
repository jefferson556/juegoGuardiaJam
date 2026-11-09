using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Piso13.Realidad;
using Kind = Piso13.Realidad.P13RealidadSpatialAuthoring.SpatialKind;

namespace Piso13.Editor
{
    public static class P13RealidadSceneConverter
    {
        public const string ScenePath = "Assets/Piso13/Scenes/Piso13 Realidad.unity";
        static readonly string[] EnvironmentNames = { "Piso13_CAM01", "CAM02_Recepcion", "CAM03_Ascensor", "CAM04_Pasillo", "CAM05_Archivo", "CAM06_Servidores", "CAM07_Tecnica", "CAM08_Abandonada", "CAM09_Almacen", "Piso12_Administrativo" };
        static Dictionary<string, GameObject> models;
        [MenuItem("Piso 13/Realidad/Convertir escena actual a editable")]
        static void ConvertMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play antes de convertir.");
            if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Abrir Piso13 Realidad antes de convertir.");
            if (!EditorUtility.DisplayDialog("Convertir Realidad", "Se creará contenido persistente y una copia de seguridad en Library/Piso13SceneBackups. No se sobrescribirá una escena ya convertida. ¿Continuar?", "Convertir", "Cancelar")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConvertCurrentScene();
        }
        static Transform Empty(string name, Transform parent = null)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Convertir Realidad");
            go.transform.SetParent(parent, false); return go.transform;
        }
        public static void Reference(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(owner); var p = so.FindProperty(field);
            if (p == null) throw new InvalidOperationException(owner.name + ": no existe " + field);
            p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner);
        }
        static void References(UnityEngine.Object owner, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(owner); var p = so.FindProperty(field); p.arraySize = values.Length;
            for (int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static GameObject Model(string name, Transform parent, bool axes)
        {
            var prefab = models[name];
            // Inspeccionar los componentes importados antes de eliminar únicamente cámaras/luces/colliders.
            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
                if (c == null) throw new InvalidOperationException("Missing Script en modelo " + name);
            Transform wrapper = axes ? Empty(name, parent) : parent;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(go, "Instanciar modelo"); go.transform.SetParent(wrapper, false);
            if (axes)
            {
                go.name = "Visual";
                var tr = go.GetComponentsInChildren<Transform>(true);
                Func<string, Transform> find = n => tr.FirstOrDefault(t => t.name == "P13_AXIS_" + n);
                var origin = find("O");
                if (origin != null && find("X") != null && find("Y") != null && find("Z") != null)
                {
                    Vector3 x=wrapper.InverseTransformVector(find("X").position-origin.position), y=wrapper.InverseTransformVector(find("Y").position-origin.position), z=wrapper.InverseTransformVector(find("Z").position-origin.position);
                    if (x.magnitude < .0001f || y.magnitude < .0001f || z.magnitude < .0001f) throw new InvalidOperationException("Ejes inválidos: " + name);
                    var rotation=Quaternion.LookRotation(z.normalized,y.normalized);
                    float sign=Vector3.Dot(Vector3.Cross(y,z),x)>=0?1:-1;
                    go.transform.localRotation=Quaternion.Inverse(rotation);
                    go.transform.localScale=new Vector3(sign/x.magnitude,1/y.magnitude,1/z.magnitude);
                    go.transform.localPosition-=wrapper.InverseTransformVector(origin.position-wrapper.position);
                }
            }
            foreach (var camera in go.GetComponentsInChildren<Camera>(true)) Undo.DestroyObjectImmediate(camera);
            foreach (var light in go.GetComponentsInChildren<Light>(true)) Undo.DestroyObjectImmediate(light);
            foreach (var listener in go.GetComponentsInChildren<AudioListener>(true)) Undo.DestroyObjectImmediate(listener);
            foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(collider);
            return axes ? wrapper.gameObject : go;
        }
        static GameObject Prop(string model, string name, Transform parent, Vector3 pos, Vector3 rot, Vector3 scale, string material=null)
        {
            var go=Model(model,parent,false); go.name=name; go.transform.localPosition=pos;
            go.transform.localRotation=Quaternion.Euler(rot); go.transform.localScale=scale;
            if (material != null)
            {
                var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Piso13/Resources/Models/Materials/"+material+".mat");
                if (mat == null) throw new InvalidOperationException("Falta material " + material);
                foreach(var r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
            }
            return go;
        }
        static BoxCollider Collider(GameObject go, Vector3 center, Vector3 size)
        {
            var c=Undo.AddComponent<BoxCollider>(go); c.center=center;c.size=size;return c;
        }
        static Material PersistentMaterial(string name, Color color, float smoothness)
        {
            const string folder="Assets/Piso13/Materials/Realidad";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string path=folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat!=null)return mat;
            mat=new Material(P13RealidadWorld.LitShader()){name=name,color=color};mat.SetFloat("_Smoothness",smoothness);
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static P13RealidadSpatialAuthoring Marker(string name, Transform parent, Kind kind, int id, int floor, Vector3 pos, Vector3 size, int priority=0, Transform target=null, int[] connections=null)
        {
            var t=Empty(name,parent);t.position=pos;var s=Undo.AddComponent<P13RealidadSpatialAuthoring>(t.gameObject);
            s.Configure(kind,id,floor,name,size,priority,target,connections); return s;
        }
        static Light PointLight(string name, Transform parent, Vector3 pos, float range, float intensity, Color color)
        {
            var t=Empty(name,parent);t.position=pos;var l=Undo.AddComponent<Light>(t.gameObject);
            l.type=LightType.Point;l.range=range;l.intensity=intensity;l.color=color;return l;
        }
        static void ImportObstacles(Obstacle[] obstacles, Kind kind, int floor, Transform config, Transform geometry)
        {
            var used=new HashSet<Renderer>();
            foreach(var pair in obstacles.Select((o,i)=>new{o,i}))
            {
                var o=pair.o;var pos=new Vector3((o.minX+o.maxX)*.5f,o.height*.5f,(o.minZ+o.maxZ)*.5f);
                var size=new Vector3(o.maxX-o.minX,o.height,o.maxZ-o.minZ);
                var s=Marker(kind+"_"+pair.i,config,kind,pair.i,floor,pos,size);
                // Vincular únicamente cuando la malla corresponde claramente al proxy importado.
                var match=geometry.GetComponentsInChildren<Renderer>(false).Where(r=>!used.Contains(r))
                    .Select(r=>new{r,score=Vector3.Distance(r.bounds.center,pos)+Vector3.Distance(r.bounds.size,size)})
                    .OrderBy(x=>x.score).FirstOrDefault();
                if(match!=null && match.score<.8f)
                {
                    s.transform.SetParent(match.r.transform,true);Reference(s,"_linkedVisual",match.r.transform);used.Add(match.r);
                }
            }
        }
        public static P13RealidadSceneBindings ConvertCurrentScene()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=ScenePath)throw new InvalidOperationException("Conversión solo en Piso13 Realidad, Edit Mode.");
            var game=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<P13RealidadGame>(true)).Single();
            var existing=game.GetComponent<P13RealidadSceneBindings>();
            if(existing!=null) {Debug.Log("Realidad ya convertida: no se sobrescribe contenido.",existing);return existing;}
            if(scene.GetRootGameObjects().Any(r=>r.name=="Piso13_Realidad_World" || r.name=="PlayerCamera" || r.name=="RealidadEnemy"))throw new InvalidOperationException("Hay contenido no inventariado: cancelar conversión para no sobrescribirlo.");
            models=AssetDatabase.FindAssets("t:Model",new[]{"Assets/Piso13/Resources/Models"}).Select(AssetDatabase.GUIDToAssetPath)
                .GroupBy(Path.GetFileNameWithoutExtension).ToDictionary(g=>g.Key,g=>AssetDatabase.LoadAssetAtPath<GameObject>(g.First()));
            foreach(var name in EnvironmentNames.Concat(new[]{"CAM01_Puerta","Piso13_HandFlashlight","MonsterGuard","telefono vintage rojo","cafetera","taza","persona elegante","Cama sola","huma-mask","Clock_Pentagram","Guardia durmiendo"}))
                if(!models.ContainsKey(name)||models[name]==null)throw new InvalidOperationException("Falta modelo requerido: "+name);
            var json=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Piso13/Resources/Data/World.json");
            if(json==null)throw new InvalidOperationException("Falta World.json");var data=JsonUtility.FromJson<WorldData>(json.text);
            Directory.CreateDirectory("Library/Piso13SceneBackups");File.Copy(ScenePath,"Library/Piso13SceneBackups/Realidad_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",false);
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Convertir Piso13 Realidad");
            try
            {
                var root=Empty("Piso13_Realidad_World");var geometry=Empty("Geometria",root);var lightsRoot=Empty("Iluminacion",root);
                var rainRoot=Empty("Lluvia",root);var propsRoot=Empty("Interactuables",root);var anomalies=Empty("Anomalias",root);var config=Empty("ConfiguracionEspacial",root);
                foreach(var name in EnvironmentNames)
                {
                    var loaded=Model(name,geometry,true);
                    if(name=="Piso13_CAM01")foreach(var t in loaded.GetComponentsInChildren<Transform>(true))if(EnvironmentNames.Skip(1).Take(8).Contains(t.name))t.gameObject.SetActive(false);
                }
                var doorRoot=Empty("PuertaOficina",root);var door=Model("CAM01_Puerta",doorRoot,true).transform;door.position=new Vector3(1.85f,1.35f,-3);
                var open=Empty("Abierta",doorRoot);open.position=door.position;
                var closed=Empty("Cerrada",doorRoot);closed.position=new Vector3(0,1.35f,-3);
                var interaction=Empty("Interaccion",doorRoot);interaction.position=new Vector3(0,.8f,-2.35f);
                ImportObstacles(data.walls,Kind.Wall,13,config,geometry);ImportObstacles(data.furniture,Kind.Furniture,13,config,geometry);ImportObstacles(data.floor12,Kind.Floor12Obstacle,12,config,geometry);
                var od=data.officeDoor;Marker("BloqueoPuerta",doorRoot,Kind.OfficeDoorObstacle,0,13,new Vector3((od.minX+od.maxX)*.5f,od.height*.5f,(od.minZ+od.maxZ)*.5f),new Vector3(od.maxX-od.minX,od.height,od.maxZ-od.minZ));
                Marker("LimitesPiso13",config,Kind.FloorBounds,13,13,new Vector3(0,1.7f,0),new Vector3(15.4f,3.4f,15.4f));
                Marker("LimitesPiso12",config,Kind.FloorBounds,12,12,new Vector3(30,1.7f,8.3f),new Vector3(12,3.4f,31.2f));
                Marker("Oficina",config,Kind.Room,1,13,new Vector3(0,1.7f,0),new Vector3(5.36f,3.4f,5.36f),40);
                Marker("Ascensor",config,Kind.Room,3,13,new Vector3(0,1.7f,-7.2f),new Vector3(2,3.4f,2),30);
                Marker("Pasillo",config,Kind.Room,4,13,new Vector3(0,1.7f,0),new Vector3(5.36f,3.4f,16.4f),20);
                Marker("Recepcion",config,Kind.Room,2,13,new Vector3(-4.1f,1.7f,0),new Vector3(8.2f,3.4f,6),10);
                Marker("Tecnica",config,Kind.Room,7,13,new Vector3(4.1f,1.7f,0),new Vector3(8.2f,3.4f,6),10);
                Marker("Archivo",config,Kind.Room,5,13,new Vector3(-4.1f,1.7f,-5.6f),new Vector3(8.2f,3.4f,5.2f),10);
                Marker("Servidores",config,Kind.Room,6,13,new Vector3(4.1f,1.7f,-5.6f),new Vector3(8.2f,3.4f,5.2f),10);
                Marker("Abandonada",config,Kind.Room,8,13,new Vector3(-4.1f,1.7f,5.6f),new Vector3(8.2f,3.4f,5.2f),10);
                Marker("Almacen",config,Kind.Room,9,13,new Vector3(4.1f,1.7f,5.6f),new Vector3(8.2f,3.4f,5.2f),10);
                Marker("Administrativo",config,Kind.Room,12,12,new Vector3(30,1.7f,8.3f),new Vector3(12,3.4f,31.2f),50);
                var lights=new List<Light>();
                for(int i=0;i<data.cameras.Length;i++)
                {
                    var c=data.cameras[i];var target=Empty("Objetivo_"+i,config);target.position=c.target;
                    Marker(c.shortName,config,Kind.CCTV,i,13,c.position,Vector3.one,0,target);
                    lights.Add(PointLight("Luz_"+c.shortName,lightsRoot,c.position+Vector3.down*.1f,8,.38f,new Color(.55f,.72f,.8f)));
                }
                foreach(var room in data.rooms12)lights.Add(PointLight("Luz_"+room.name,lightsRoot,room.position+Vector3.up*2,7,.32f,new Color(.55f,.72f,.8f)));
                PointLight("LuzPuertaOficina",doorRoot,new Vector3(0,2.7f,-3.6f),8.5f,.65f,new Color(1,.96f,.9f));
                int[][] connections={new[]{1,6,3},new[]{0,7},new[]{3},new[]{0,2,4,5,9},new[]{3,7},new[]{3,8},new[]{0,8},new[]{1,4},new[]{6,5},new[]{3}};
                for(int i=0;i<P13RealidadEnemy.destinations.Length;i++)Marker("Patrulla_"+i,config,Kind.Patrol,i,13,P13RealidadEnemy.destinations[i],Vector3.one,0,null,connections[i]);
                var maskPositions=new[]{new Vector3(0,1.35f,-.8f),new Vector3(-6.8f,1.35f,-.6f),new Vector3(0,1.35f,-7.55f),new Vector3(0,1.35f,-4.5f),new Vector3(-6.2f,1.35f,-5.9f),new Vector3(6.2f,1.35f,-5.9f),new Vector3(6.8f,1.35f,-.6f),new Vector3(-6.8f,1.35f,6.2f),new Vector3(6.8f,1.35f,6.2f)};
                float[] maskYaws={180,90,180,0,90,-90,-90,90,-90};
                for(int i=0;i<9;i++){var m=Marker("MascaraSpawn_"+(i+1),config,Kind.MaskSpawn,i+1,13,maskPositions[i],Vector3.one);m.transform.rotation=Quaternion.Euler(0,maskYaws[i],0);}
                var rainMat=PersistentMaterial("M_Lluvia",new Color(.19f,.26f,.32f),.2f);var windows=new List<Transform>();
                foreach(var p in new[]{new Vector3(-7.68f,1.85f,5.2f),new Vector3(-7.68f,1.85f,.4f),new Vector3(24,1.75f,7.3f),new Vector3(36,1.75f,7.3f),new Vector3(24,1.75f,11.5f),new Vector3(36,1.75f,11.5f),new Vector3(24,1.75f,-2.4f),new Vector3(36,1.75f,2.6f)})
                {
                    var w=Empty("LluviaVentana_"+windows.Count,rainRoot);w.position=p;windows.Add(w);
                    for(int i=0;i<14;i++){var drop=GameObject.CreatePrimitive(PrimitiveType.Cube);Undo.RegisterCreatedObjectUndo(drop,"Gota");drop.name="Gota";drop.transform.SetParent(w,false);drop.transform.localPosition=new Vector3(.01f,(i*.397f)%1.5f-.75f,(i*.173f)%2-.99f);drop.transform.localScale=new Vector3(.006f,.1f,.006f);Undo.DestroyObjectImmediate(drop.GetComponent<Collider>());drop.GetComponent<Renderer>().sharedMaterial=rainMat;}
                }
                var spawn=Marker("PlayerSpawn",config,Kind.Spawn,0,13,new Vector3(-5.6f,.8f,-6),Vector3.one).transform;spawn.rotation=Quaternion.Euler(0,90,0);
                var cameraGo=Empty("PlayerCamera");var view=Undo.AddComponent<Camera>(cameraGo.gameObject);view.tag="MainCamera";view.nearClipPlane=.035f;view.farClipPlane=50;view.fieldOfView=72;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;Undo.AddComponent<AudioListener>(cameraGo.gameObject);cameraGo.position=new Vector3(spawn.position.x,1.65f,spawn.position.z);cameraGo.rotation=spawn.rotation;
                var flash=Undo.AddComponent<Light>(Empty("Linterna",cameraGo).gameObject);flash.transform.localPosition=new Vector3(.18f,-.16f,.55f);flash.type=LightType.Spot;flash.range=22;flash.spotAngle=55;flash.innerSpotAngle=24;flash.intensity=12;flash.color=new Color(1,.95f,.87f);flash.shadows=LightShadows.Soft;
                var hand=Model("Piso13_HandFlashlight",cameraGo,true).transform;
                foreach(var renderer in hand.GetComponentsInChildren<Renderer>(true))renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var enemySpawn=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="EnemySpawn");
                if(enemySpawn==null){enemySpawn=Empty("EnemySpawn",config).gameObject;enemySpawn.transform.position=new Vector3(6.8f,.8f,6.2f);enemySpawn.transform.rotation=Quaternion.Euler(0,180,0);}else{Undo.RecordObject(enemySpawn.transform,"Enlazar spawn");enemySpawn.transform.SetParent(config,true);}
                var enemyGo=Empty("RealidadEnemy");var enemy=Undo.AddComponent<P13RealidadEnemy>(enemyGo.gameObject);enemy.Game=game;
                var enemyRoot=Empty("MonsterGuard_Root",enemyGo);var visual=Model("MonsterGuard",enemyRoot,true).transform;
                var rs=visual.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);float factor=enemy.characterHeight/b.size.y;visual.localScale=Vector3.one*factor;visual.localPosition=new Vector3(0,-.8f-b.min.y*factor,0);visual.localRotation=Quaternion.Euler(0,180,0);
                foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(new Vector3(0,1.1f,0),new Vector3(2.5f,2.5f,2.5f));}
                enemy.Root=enemyRoot;enemy.Visual=visual;enemy.Anim=visual.GetComponentInChildren<Animation>();Reference(enemy,"_spawnPoint",enemySpawn.transform);enemyRoot.position=enemySpawn.transform.position;enemyRoot.rotation=enemySpawn.transform.rotation;
                enemy.SetupAudio();enemyGo.gameObject.SetActive(false);
                var phoneGo=Prop("telefono vintage rojo","Telefono_Vintage_Rojo",propsRoot,game.PhonePosition,game.PhoneRotation,game.PhoneScale,"M_TelefonoVintage");Collider(phoneGo,new Vector3(0,.4f,0),new Vector3(1.6f,1,1.6f));var phone=Undo.AddComponent<P13VintagePhone>(phoneGo);phone.Game=game;phone.ProximityDistance=game.PhoneTriggerDistance;phone.ExitDistance=game.PhoneExitDistance;phone.SetupAudioSources();
                var cafe=Prop("cafetera","Cafetera",propsRoot,new Vector3(-7.287f,1.111f,1.3164f),new Vector3(0,100.82f,0),Vector3.one*.5f,"M_Cafetera");Collider(cafe,new Vector3(0,.45f,0),new Vector3(1.1f,1,1.1f));var report=Undo.AddComponent<P13ReportableProp>(cafe);report.PropId="Cafetera";
                var cupGo=Prop("taza","Taza_Cafe",propsRoot,new Vector3(-7.287f,1.111f,1.038f),Vector3.zero,Vector3.one*.2f,"M_Taza");Collider(cupGo,new Vector3(0,.35f,0),new Vector3(.9f,.75f,.8f));var cup=Undo.AddComponent<P13CoffeeCup>(cupGo);var cupAnomaly=Undo.AddComponent<P13Anomaly>(cupGo);cupAnomaly.AnomalyId="Taza de Café";cupAnomaly.EnemySpeedMultiplier=1;
                var liquid=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Undo.RegisterCreatedObjectUndo(liquid,"Líquido");liquid.name="Liquid_Coffee";liquid.transform.SetParent(cupGo.transform,false);liquid.transform.localPosition=new Vector3(0,cup.fullHeight,0);liquid.transform.localScale=new Vector3(.38f,.015f,.38f);Undo.DestroyObjectImmediate(liquid.GetComponent<Collider>());var liquidMat=PersistentMaterial("M_CafeLiquido",new Color(.14f,.06f,.02f),.92f);liquid.GetComponent<Renderer>().sharedMaterial=liquidMat;Reference(cup,"liquid",liquid.transform);Reference(cup,"liquidMaterial",liquidMat);cup.InitLiquid();
                var personaGo=Prop("persona elegante","Persona_Elegante_CAM06",anomalies,new Vector3(5.89f,.08f,-5.13f),new Vector3(0,-18.5f,0),Vector3.one*2,"M_PersonaElegante");Collider(personaGo,new Vector3(0,.9f,0),new Vector3(.7f,1.8f,.7f));var persona=Undo.AddComponent<P13PersonaEleganteAnomaly>(personaGo);persona.BaseRotation=personaGo.transform.localRotation;personaGo.SetActive(false);
                var bed=Prop("Cama sola","Cama_Sola",propsRoot,new Vector3(-6.712f,-.014f,-6.003f),Vector3.zero,Vector3.one*2);
                var structure=AssetDatabase.LoadAssetAtPath<Material>("Assets/Piso13/Resources/Models/Materials/M_CamaSola.mat");var linen=AssetDatabase.LoadAssetAtPath<Material>("Assets/Piso13/Resources/Models/Materials/M_CamaSola_Part2.mat");
                foreach(var r in bed.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(r.name.Contains("Colchon")||r.name.Contains("Almohada")||r.name.Contains("Manta")?linen:structure,r.sharedMaterials.Length).ToArray();Collider(bed,new Vector3(0,.2f,.02f),new Vector3(.55f,.4f,1.05f));
                var guard=Prop("Guardia durmiendo","Guardia_Durmiendo",propsRoot,bed.transform.localPosition,Vector3.zero,bed.transform.localScale);
                var gp1=AssetDatabase.LoadAssetAtPath<Material>("Assets/Piso13/Resources/Models/Materials/M_GuardiaDurmiendo_Part1.mat");var gp2=AssetDatabase.LoadAssetAtPath<Material>("Assets/Piso13/Resources/Models/Materials/M_GuardiaDurmiendo_Part2.mat");foreach(var r in guard.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Length>=2?new[]{gp1,gp2}:new[]{gp2};guard.SetActive(false);var endTarget=Empty("ObjetivoFinal",bed.transform);endTarget.position=new Vector3(-6.712f,.35f,-6.003f);
                var maskGo=Prop("huma-mask","Mascara_Diablo_Huma",anomalies,Vector3.zero,Vector3.zero,Vector3.one*.9f,"M_MascaraDiabloHuma");Collider(maskGo,Vector3.zero,new Vector3(.9f,1,.8f));var mask=Undo.AddComponent<P13HumanMaskAnomaly>(maskGo);mask.Game=game;mask.InitDefaultSpawnPoints();mask.SetVisuals(false);
                var clockGo=Prop("Clock_Pentagram","Clock_Pentagram",anomalies,game.ClockAnomalyPosition,game.ClockAnomalyRotation,game.ClockAnomalyScale);Collider(clockGo,new Vector3(0,0,.02f),new Vector3(.35f,.35f,.12f));var clock=Undo.AddComponent<P13Anomaly>(clockGo);clock.AnomalyId="Reloj Pentagrama";clock.EnemySpeedMultiplier=1;Undo.AddComponent<P13ClockHandsRotator>(clockGo).FindHands();clockGo.SetActive(false);
                var station=Undo.AddComponent<P13CCTVStation>(Empty("camarasComputador",propsRoot).gameObject);station.Game=game;station.transform.position=station.DeskCoordinates;var st=new SerializedObject(station);st.FindProperty("_useSceneTransform").boolValue=true;st.ApplyModifiedPropertiesWithoutUndo();station.SetupCollider();
                var bindings=Undo.AddComponent<P13RealidadSceneBindings>(game.gameObject);
                foreach(var pair in new Dictionary<string,UnityEngine.Object>{{"_worldRoot",root},{"_officeDoor",door},{"_doorOpen",open},{"_doorClosed",closed},{"_doorInteraction",interaction},{"_view",view},{"_flashlight",flash},{"_hand",hand},{"_playerSpawn",spawn},{"_enemySpawn",enemySpawn.transform},{"_enemy",enemy},{"_phone",phone},{"_coffee",cup},{"_persona",persona},{"_mask",mask},{"_clock",clock},{"_station",station},{"_bed",bed},{"_sleepingGuard",guard},{"_endingTarget",endTarget}})Reference(bindings,pair.Key,pair.Value);
                References(bindings,"_lights",lights.Cast<UnityEngine.Object>().ToArray());References(bindings,"_windows",windows.Cast<UnityEngine.Object>().ToArray());References(bindings,"_spatial",root.GetComponentsInChildren<P13RealidadSpatialAuthoring>(true).Cast<UnityEngine.Object>().ToArray());
                var bs=new SerializedObject(bindings);bs.FindProperty("_initialData").boxedValue=data;bs.ApplyModifiedPropertiesWithoutUndo();Reference(game,"_sceneBindings",bindings);
                game.View=view;game.Enemy=enemy;game.CoffeeCup=cup;game.PersonaAnomaly=persona;game.HumanMaskAnomaly=mask;game.CCTVStation=station;game.Data=data;
                var errors=bindings.ValidateScene();if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.025f,.032f,.045f);RenderSettings.reflectionIntensity=.2f;
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.015f,.022f,.03f);RenderSettings.fogDensity=.015f;
                EditorUtility.SetDirty(game);EditorUtility.SetDirty(bindings);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(undo);
                Selection.activeGameObject=root.gameObject;Debug.Log("Piso13 Realidad convertida y guardada.",bindings);return bindings;
            }
            catch {Undo.RevertAllDownToGroup(undo);throw;}
        }
        [MenuItem("Piso 13/Realidad/Validar escena editable")]
        public static void ValidateCurrentScene()
        {
            var b=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<P13RealidadSceneBindings>(true)).FirstOrDefault();
            if(b==null){Debug.LogError("Esta escena no tiene bindings de Realidad.");return;}
            var errors=b.ValidateScene();foreach(var e in errors)Debug.LogError(e,b);if(errors.Count==0)Debug.Log("Referencias espaciales válidas.",b);
        }
    }
}
