package
{
	import away3d.cameras.HoverCamera3D;
	import away3d.containers.ObjectContainer3D;
	import away3d.containers.View3D;
	import away3d.core.base.Object3D;
	import away3d.core.base.Vertex;
	import away3d.events.MouseEvent3D;
	import away3d.lights.DirectionalLight3D;
	import away3d.loaders.data.ContainerData;
	import away3d.materials.PhongColorMaterial;
	import away3d.materials.WireColorMaterial;
	import away3d.materials.WireframeMaterial;
	import away3d.primitives.Cone;
	import away3d.primitives.Cylinder;
	import away3d.primitives.LineSegment;
	import away3d.primitives.Sphere;
	
	import com.adobe.serialization.json.JSON;
	import com.bit101.components.InputText;
	import com.bit101.components.Label;
	import com.bit101.components.PushButton;
	import com.bit101.components.Text;
	import com.greensock.TweenMax;
	import com.greensock.easing.EaseLookup;
	import com.spikything.utils.MouseWheelTrap;
	
	import flash.desktop.Clipboard;
	import flash.desktop.ClipboardFormats;
	import flash.display.Scene;
	import flash.display.Sprite;
	import flash.display.StageAlign;
	import flash.display.StageScaleMode;
	import flash.events.Event;
	import flash.events.FocusEvent;
	import flash.events.KeyboardEvent;
	import flash.events.MouseEvent;
	import flash.geom.Vector3D;
	import flash.net.URLLoader;
	import flash.net.URLRequest;
	import flash.system.System;
	import flash.ui.ContextMenu;
	import flash.utils.getTimer;
	
	import mx.events.FlexEvent;

	[SWF(frameRate="30", backgroundColor="#FFFFFF")]
	public class Structure3D extends Sprite
	{
		private var view:View3D;
		private var cover:Cover;
		private var cam:HoverCamera3D;
		private var label: Label;
		private var label1:Label;
		private var label2:Label;
		private var label3:Label;
		private var info: Label;
		private var flag:String = "solid";;
		private var lastTime:Number;
		private var cell: ObjectContainer3D;
		//		private var host: String = "localhost";
		private var factor: Number = 15000;
		private var host: String;
		private var len: Number;
		private var nodesLength: Number;
		private var shape: String;
		// camera variables
		private var move:Boolean = false;
		private var lastPanAngle:Number;
		private var lastTiltAngle:Number;
		private var lastMouseX:Number;
		private var lastMouseY:Number;
		private var light: DirectionalLight3D;
		private var id: String;
		private var camObject: Object3D;
		private var resetObject: Object3D;
		private var Xcount: Number;
		private var Ycount : Number;
		private var edgeDict: Array;
		private var hfactor: Number;
		private var alert: Cover;
		private var stareObject:*;
		
		private var labName: String;
		private var dataSource: String;
		
		private var nodeColor: String;
		
		private var clipboardText: String;
		
		private var copyBox: Text;
		
		private var averageRadius: Number;
		
		private var tinyObject: *;
		
		
		public function Structure3D()
		{
			super();
			MouseWheelTrap.setup(stage);
			Xcount = 8;
			Ycount = 12;
			stage.align = StageAlign.TOP_LEFT;	
			stage.scaleMode = StageScaleMode.NO_SCALE;
			//			var loader: URLLoader = new URLLoader(new URLRequest("http://localhost/Viz/Structure/StructuresJSON/180"));
			//			loader.addEventListener(Event.COMPLETE,Work);
			init3D();
			alert = new Cover(this,stage.stageWidth,stage.stageHeight,"Receiving data, please wait...");
			addChild(alert);
			view.render();
			
			//comment this part for testing
						getHost();
			
			// uncomment this part for production
//			this.host = "155.100.105.9/Test";
//			this.shape = "cylinder";		
//			this.hfactor = 0.25;
//			this.getData("MarcLab(connectome.utah.edu),Rabbit(MarcLab),180");
			
		}
		public function getHost(): void
		{
			var regexp: RegExp = /.*\//;
			var hostregex: RegExp = /http:\/\/(.*?)\/(.*?)\//;
			var temphost: Array = hostregex.exec(root.loaderInfo.loaderURL);
			this.host =  temphost[1] +"/"+ temphost[2];
			var loader: URLLoader = new URLLoader(
				new URLRequest(regexp.exec(root.loaderInfo.loaderURL) +"flash_config.xml"));
			loader.addEventListener(Event.COMPLETE, getHostName);
		}
		
		public function getHostName(event: Event): void
		{
			var xml: XML = new XML(event.target.data);
			this.shape = xml.shape;		
			this.hfactor = xml.hfactor;
			trace(host);
			trace(shape);
			trace(hfactor);
			
			var loader: URLLoader = new URLLoader(new URLRequest("http://"+host+"/Structure/GetID"));
			loader.addEventListener(Event.COMPLETE,fileGenerated);				
		}
		
		
		public function getID(): void
		{
			var loader: URLLoader = new URLLoader(new URLRequest("http://"+host+"/Structure/GetID"));
			loader.addEventListener(Event.COMPLETE,fileGenerated);			
		}
		
		public function fileGenerated(event: Event): void
		{
			var path: String = new String(event.target.data);
			var loader: URLLoader = new URLLoader(new URLRequest(path));
			loader.addEventListener(Event.COMPLETE,fileLoaded);
			
		}
		/**
		 * 
		 * @param event
		 * 
		 */
		public function fileLoaded(event: Event): void
		{
			var string:* =  new Array();
			var xml: XML = new XML(event.target.data);			
			trace(xml);
			getData(xml.toString());
		}
		
		public function getData(parsedInfo: String): void
		{
			
			trace("http://"+host+"/Structure/StructuresJSON?request="+ parsedInfo);
			var loader: URLLoader = new URLLoader(new URLRequest("http://"+host+"/Structure/StructuresJSON?request="+parsedInfo));
			loader.addEventListener(Event.COMPLETE,Work);	
			
		}
		
		public function init3D(): void
		{
			cam = new HoverCamera3D({zoom:60, focus:200});
			cam.panAngle  = 180;
			cam.tiltAngle = 5 ;
			cam.minTiltAngle = -90;	
			
			
			cam.distance = 800;
			
			// create a viewport
			view = new View3D({camera:cam});
			view.x = stage.stageWidth / 2;
			view.y = stage.stageHeight / 2;
			addChild(view);			
			
			
		}
		
		public function handleKeypress(event: KeyboardEvent): void{
//			
//			if(event.ctrlKey && event.keyCode == 65)
//				
//				trace("CTRL A is pressed");
//			
//			if(event.ctrlKey && event.keyCode == 67)
//				
//				trace("CTRL C is pressed");			
//			
			
		}
		
		
		
		
     protected function setContextMenuPasteOnly(event:FlexEvent):void
     {
         var menu : ContextMenu = new ContextMenu();
             menu.hideBuiltInItems();
             menu.clipboardItems.paste = true;
             menu.clipboardItems.selectAll = false;
             menu.clipboardMenu = true;
         contextMenu = menu;
        }

		public function Work(event: Event):void
		{
			var graph: Object = JSON.decode(event.target.data);	
			
			trace(event.target.data);		
			
			//			var colors: Array =  new Array(0xF64A15,0xF0D804,0x52F004,0x01CE8D,0x01ACCE,0x2F17E4,0xB817E4,0xE417B1,0xE4173A);					
			
			var colors: Array =  new Array(0x99CC00);
			
			var dict: Array = new Array();
			
			var synapseDict: Array = new Array();
			
			edgeDict = new Array();
			
			cell = new ObjectContainer3D();
			view.scene.addChild(cell);
			
			nodeColor = graph.defaultNodeColor;
			var halflen: Number;
			camObject = new Object3D();
			
			
			var first: Boolean = new Boolean(true);
			
			averageRadius = 0;
			len = graph.Nodes.length;
			nodesLength = graph.Nodes.length;
			halflen = len/2;
			var colorslen: int = new int(colors.length);
			
			
			
			for(var i:int=0; i<len; i++)
			{
				var node:* = graph.Nodes[i];
				dict[node.ID] = node;
				
				var sphere: *;
				if(hfactor == 0.0)
					hfactor = 0.2;
				if(shape == "cylinder")
				{
					sphere = new Cylinder();
					sphere.segmentsH = 1;
					sphere.segmentsW = 8;
					sphere.height = node.radius*hfactor;
				}
				else if(shape == "sphere")
				{
					sphere = new Sphere();
				}
				else
				{
					sphere = new Cone();
					sphere.height = node.radius*hfactor;
				}					
				
				
				// fix floating point precision in flash10
							
				var factor: int = 1;
				sphere.moveTo(node.location[0],-node.location[2], node.location[1]);
				sphere.radius = node.radius;
				averageRadius += node.radius;
				
				
				sphere.extra = node.ID;
				var rand: Number = Math.round(Math.random()* colorslen-1);
				var wireWidth: Number = node.radius*0.1;
				var outline: WireframeMaterial= new WireframeMaterial(0x000000);
				
				var sphereMaterial: WireColorMaterial = new WireColorMaterial(0x330000, {thickness:wireWidth});				
				//				var sphereMaterial:PhongColorMaterial = new PhongColorMaterial(0x330000);
				
				sphere.material = sphereMaterial;
				sphereMaterial.color = graph.defaultNodeColor;
				outline.thickness = 2.0;
				sphere.bothsides = true;						
				sphere.name = "sphere"+i;
				sphere.addEventListener(MouseEvent3D.MOUSE_DOWN, sphereDetails);
				sphere.addEventListener(MouseEvent3D.ROLL_OVER, sphereFocus);
				sphere.addEventListener(MouseEvent3D.ROLL_OUT, sphereOut);
				cell.addChild(sphere);
				if(i == 1)
				{
					camObject = sphere;
					resetObject = sphere;
					cam.target = camObject;
					//					cam.lookAt(new Vector3D(sphere.x, sphere.y, sphere.z));
				}
				cam.moveTo(sphere.x, sphere.y+2000, sphere.z);
				
				stareObject = sphere;
			}
			averageRadius /= len;
			var synapseLen:* = graph.Synapses.length;
			
			for(i=0; i<synapseLen; i++)
			{
				var synapse:* = graph.Synapses[i];
				synapseDict[synapse.ID] = synapse;
				
				var sphere: *;
				hfactor=0.2;
				if(hfactor == 0.0)
					hfactor = 0.1;
				sphere = new Cone();
				
				sphere.height = synapse.radius;
				
				
				var factor: int = 1;
				sphere.moveTo(synapse.location[0],-synapse.location[2], synapse.location[1]);
				sphere.radius = synapse.radius;
				
				
				sphere.extra = " Synapse: "+synapse.ID +", " + synapse.type;
				var rand: Number = Math.round(Math.random()* colorslen-1);
				var wireWidth: Number = synapse.radius*0.1;
				var outline: WireframeMaterial= new WireframeMaterial(0x000000);
				var sphereMaterial: WireColorMaterial = new WireColorMaterial(synapse.color);						
				//				var sphereMaterial:PhongColorMaterial = new PhongColorMaterial(0x330000);
				sphere.material = sphereMaterial;
				sphereMaterial.color = synapse.color.toString();
				outline.thickness = 2.0;
				sphere.bothsides = true;						
				sphere.name = "sphere"+i;
				sphere.addEventListener(MouseEvent3D.MOUSE_DOWN, sphereDetails);
				sphere.addEventListener(MouseEvent3D.ROLL_OVER, sphereFocus);
				sphere.addEventListener(MouseEvent3D.ROLL_OUT, sphereOut);
				cell.addChild(sphere);
				
			}
			
			
			light = new DirectionalLight3D();
			var light2:DirectionalLight3D = new DirectionalLight3D();
			light2.direction=new Vector3D(0,cam.position.y-100, 0);
			
			
			var light3: DirectionalLight3D  = new DirectionalLight3D();
			light3.direction = new Vector3D(cam.position.x-100,0,0);
			
			var light4: DirectionalLight3D = new DirectionalLight3D();
			light4.direction= new Vector3D(0,0,cam.position.z-100);
			
			
			// Move the light away from the default 0,0,0 position so we'll see some reflection
			light.direction = cam.position;
			light.ambient =0.2;
			light.diffuse = 0.2;
			light.specular = 0.2;
			light.brightness = 7.5;
			//			view.scene.addChild(light);
			
			
			len = graph.Edges.length;
			halflen =  len/3;
			for(i = 0; i<len ; i++)
			{
				var edge:* = graph.Edges[i];
				var lineMaterial: WireframeMaterial =  new WireframeMaterial(0x000000, {thickness:1.5})
				var line:LineSegment = new LineSegment({material: lineMaterial});
				
				line.start = new Vertex(dict[edge.A].location[0], -dict[edge.A].location[2], dict[edge.A].location[1]);
				line.end = new Vertex(dict[edge.B].location[0], -dict[edge.B].location[2], dict[edge.B].location[1]);
				line.extra = "Edge Connects IDs: "+ edge.A +"-"+edge.B;
				line.name = edge.distance;	
				line.addEventListener(MouseEvent3D.MOUSE_DOWN, lineDetails);
				line.addEventListener(MouseEvent3D.ROLL_OVER, displayTooltip);
				line.addEventListener(MouseEvent3D.ROLL_OUT, removeTooltip);
				cell.addChild(line);
				
			}
			
			cell.scale(20);		
			cam.hover();
			view.render();
			removeChild(alert)
			cover = new Cover(this,stage.stageWidth,stage.stageHeight,"Rendering paused, hover over to continue.");
			addChild(cover);
			addControls();
			
			info.text = "Info: choose 'Mesh' or 'Solid' for smooth display";
			// render on enterframe
			this.addEventListener(Event.ENTER_FRAME,render);
			stage.addEventListener(MouseEvent.MOUSE_DOWN, MouseDown);
			stage.addEventListener(MouseEvent.MOUSE_UP, MouseUp);	
			stage.addEventListener(MouseEvent.MOUSE_WHEEL, changeZoom);	
			
//			stage.addEventListener(Event.COMPLETE, setContextMenuOnly);
			
			stage.addEventListener(Event.PASTE, copyClipboard);
			view.scene.addEventListener(Event.PASTE, copyClipboard);
			view.addEventListener(Event.PASTE, copyClipboard);
			cell.addEventListener(Event.PASTE, copyClipboard);
			
	
			
			
		}
		
		/**
		 * 
		 * @param event
		 * 
		 */
		public function copyClipboard(event: Event): void
		{
			var sorryMessage: String = "Sorry, try clicking the button again and hit 'CTRL + V' to paste";
			var invalidMessage: String = "Invalid clipboard data";
			try
			{
			 clipboardText  = Clipboard.generalClipboard.getData(ClipboardFormats.TEXT_FORMAT) as String;
			 
			 if(clipboardText.indexOf(' ') != -1)
			 {
				 var length: int = clipboardText.length;
				 
				 if(length>20)
					 length = 20;
				 
				 var arr: * = new Array();
				 arr = clipboardText.split('\t');
				 
				 var newPosition: Vector3D = new Vector3D();
				 var found: Boolean = true;
				 var positions: Array = new Array();
				 var count:int = 0;
				 
				 for(var i:* in arr)
				 {
					 if(arr.length>3 )
					 {
						 var tempArr: * = arr[i].split(':');
						 if(tempArr.length == 2)
						 {
							 if(tempArr[0] == 'X' || tempArr[0] == 'Y' || tempArr[0] == 'Z')
							 {
								 positions[count] = tempArr[1].toString().replace(' ','');
								 count++;
							 }													 
						 }
						 
					 }
					 else
					 {
						 info.text = invalidMessage;
						 break;
					 }
					 
					 
				 }
				 
				 if(count == 3) // if coordinates were correct
				 {
					 
					
					 newPosition.x = Number(positions[0]);
					 newPosition.y = Number(positions[1]);
					 newPosition.z = Number(positions[2]);
					 var newObjectLocation: Vector3D = find3DCoordinates(newPosition);
					 
					 tinyObject = new Cone();
					 tinyObject.radius = averageRadius/4;
					 tinyObject.yUp = true;
					 
					
					 tinyObject.segmentsH = 1;
					 tinyObject.segmentsW = 8;
					 tinyObject.height = averageRadius*6;
					 
					 var material: WireColorMaterial = new WireColorMaterial(0xFF0000, {thickness:averageRadius/100});				
					 //				var sphereMaterial:PhongColorMaterial = new PhongColorMaterial(0x330000);
					 
					 tinyObject.material = material;
					 
					 var factor: int = 1;
					 tinyObject.moveTo(newObjectLocation.x, -newObjectLocation.z, newObjectLocation.y);	
					 
					 tinyObject.addEventListener(MouseEvent3D.MOUSE_DOWN, tinyObjectDetails);
					 tinyObject.addEventListener(MouseEvent3D.ROLL_OVER, sphereFocus);
					 tinyObject.addEventListener(MouseEvent3D.ROLL_OUT, sphereOut);
					 
					 cell.addChild(tinyObject);
					 cam.moveTo(newObjectLocation.x, -newObjectLocation.z, newObjectLocation.y);
					  cam.target= tinyObject;
					 cam.distance = 50;
					 cam.hover();
					 view.render();
					 
				 }
				
				 
				 
			 } 
			 else
			 {
				 info.text = invalidMessage;
			 }
			}
			catch(exception : *)
			{		
				info.text = sorryMessage;
			}
 
		}
		
		private function find3DCoordinates(number: Vector3D): Vector3D
		{
			var long: int =15000;
			
			var temp: Vector3D = new Vector3D();
			temp.x = number.x *2.18 /factor;
			temp.y = number.y *2.18 /factor;
			temp.z = number.z *90  /factor;
			
			return temp;
			
		}
		private function findVikingCoordinates(number: Vector3D): Vector3D{
			
			
			var temp: Vector3D = new Vector3D();
			temp.x = number.x*factor/2.18;
			temp.y = number.z*factor/2.18;
			temp.z = -number.y*factor/90;
			
			if(temp.z - int(temp.z) > 0.5)
				temp.z = int(temp.z) + 1;
			else
				temp.z = int(temp.z);
			
			return temp;
		}
		
		private function sphereFocus(event:Event): void
		{
			event.target.material.thickness +=3;
		}
		
		private function sphereOut(event:Event): void
		{
			event.target.material.thickness-=3;	
		}
		
		private function displayTooltip(event:Event): void
		{
			event.target.material.thickness += 3;
			event.target.material.wireColor = 0xff0000;
		}
		private function removeTooltip(event:Event): void
		{
			event.target.material.thickness -= 3;
			event.target.material.wireColor = 0x000000;
		}
		
		private function computeZLevel(num: Number): int{
			
			var integer: int = int(num);
			
			if(num - integer > 0.5)
				integer+=1
				
			return integer;
		}
		private function tinyObjectDetails(event: Event): void
		{
			
			var obj:* = event.target;
			//			obj.outline = new WireframeMaterial("red", {width:1});
			info.text = "Info: X,Y,Z copied to Clipboard";
			label.text = "That was a user Location";
			label1.text = "Z level: " + computeZLevel(-event.target.y*factor/90);		
			
			cell.removeChild(tinyObject);	
			resetCamera(event);
			view.render();
		}
		
		private function sphereDetails(event: Event): void
		{
			
			var obj:* = event.target;
			//			obj.outline = new WireframeMaterial("red", {width:1});
			info.text = "Info: X,Y,Z copied to Clipboard";
			label.text = "Location ID:"+obj.extra;
			label1.text = "Z level: " + -event.target.y*factor/90;
			var temp:Vector3D = findVikingCoordinates(new Vector3D(event.target.x,event.target.z, event.target.y));
			
			System.setClipboard(event.target.x*factor/2.18+ "\t" + event.target.z*factor/2.18 +"\t"+computeZLevel(-event.target.y*factor/90));			
//			System.setClipboard(temp.x +" "+temp.y+" "+temp.z);
			if (flag == "mesh")
			{
//				obj.material = new WireColorMaterial(nodeColor, {thickness:2.0});
			}
			else
			{
				try
				{
					obj.material = new WireframeMaterial(obj.material.color, {thickness:1.0});
				}
				catch(exception:*)
				{
					trace(exception);
				}
				
			}
			
			
			camObject = obj;		
			cam.target = camObject;		
		
			view.render();
		}
		
		private function lineDetails(event: Event): void
		{
			var obj:* = event.target;
			obj.outline = new WireframeMaterial("red", {width:1});
			label1.text = "Distance:"+obj.name +" nm";
			label.text = obj.extra;
			info.text = "Info: Selected edge";
			
			
		}
		
		private function moveCamera(): void{
			
			if(!cover.visible)
			{
				var fps:Number = Math.floor(1000/(getTimer()-lastTime) );
				
				cell.rotationY += 0.05;
				label2.text = fps+"fps, zoom/distance:" + cam.zoom;
				lastTime = getTimer();
				var cameraSpeed:Number = 0.3; // Approximately same speed as mouse movement.
				if (move) {
					cam.panAngle = cameraSpeed*(camObject.x - lastMouseX) + lastPanAngle;
					cam.tiltAngle = cameraSpeed*(camObject.y - lastMouseY) + lastTiltAngle;
				}
				cam.hover();  			    
				view.render();
			}  
			
			
		}
		
		private function render(e:Event):void
		{
			if(!cover.visible)
			{
				var fps:Number = Math.floor( 1000/(getTimer()-lastTime) );
				
				
				
				cell.rotationY += 0.05;
				label2.text = fps+"fps, zoom/distance:" + cam.zoom;
				lastTime = getTimer();
				var cameraSpeed:Number = 0.3; // Approximately same speed as mouse movement.
				if (move) {
					cam.panAngle = cameraSpeed*(stage.mouseX - lastMouseX) + lastPanAngle;
					cam.tiltAngle = cameraSpeed*(stage.mouseY - lastMouseY) + lastTiltAngle;
				}
				cam.hover();  			    
				view.render();
			}  
			
			
		}
		
		private function addControls():void
		{
			var pad:Number = 200;
			
			
			label = new Label(this, pad, pad-100);
			label.autoSize = true;
			
			label1 = new Label(this, pad, pad-80);
			label1.autoSize = true;
			
			info = new Label(this,pad, pad-60);
			info.autoSize = true;
			
			label3 = new Label(this, pad,pad-20);	
			label3.text ="ZOOM (+/-)";
			
			var plusButt:PushButton = new PushButton(this, pad, pad, "+", zoomin);
			plusButt.width = plusButt.height = 20;
			var minButt:PushButton = new PushButton(this, pad+30,pad, "-", zoomout);
			minButt.width = minButt.height = 20;
			
			label3 = new Label(this, pad,pad+20);	
			label3.text ="Display Type";
			var plusButt1:PushButton = new PushButton(this, pad, pad+40, "Mesh", mesh);
			var minButt1:PushButton = new PushButton(this, pad,pad+70, "Solid", solid);
			//			var button:PushButton = new PushButton(this, pad,pad+100, "Phong", phong);
			var copyButton: PushButton =  new PushButton(this, pad, pad + 180, "PASTE");
			var resetButton: PushButton = new PushButton(this, pad,pad+140, "RESET Camera", resetCamera);
			plusButt1.width = 40;
			minButt1.width = 40;
			//			button.width = 40;
			resetButton.width = 60;
			copyButton.width = 40;
			
			
			label2 = new Label(this, pad, pad-40);
			label2.autoSize = true;		
			
			addChild(label1);
			addChild(label2);
			//			addChild(leftButt);
			//			addChild(singleButt);
		}
		
		private function zoomin(event:Event): void
		{
			cam.distance -= 100;
			
		}
		private function zoomout(event:Event): void
		{
			cam.distance += 100;
		}
		private function mesh(event:Event): void
		{
			flag="mesh";
			
			for(var i:Number = 0; i< nodesLength ; i++)
			{
				var obj:* = view.scene.getChildByName("sphere"+i);
				var color:*;
				try{
					color = obj.material.color;
				}
				catch(e:*)
				{
					color = obj.material.wireColor;
				}
				obj.material = new WireframeMaterial( color, {thickness:1.0});			
		
			}
			view.render();
		}
		private function solid(event:Event): void
		{
			flag = "solid";
			
			for(var i:Number = 0; i< nodesLength ; i++)
			{
				var obj:* = view.scene.getChildByName("sphere"+i);
				
				var color:*;
				try{
					color = obj.material.color;
				}
				catch(e:*)
				{
					color = obj.material.wireColor;
				}			
				
				
				obj.material = new WireColorMaterial(color, {thickness:obj.height*0.1});				
			}
			view.render();
		}
		
		private function phong(event:Event): void
		{
			flag="phong";
			
			for(var i:Number = 0; i<len ; i++)
			{
				var obj:* = view.scene.getChildByName("sphere"+i);
				
				
				var color:*;
				try{
					color = obj.material.color;
				}
				catch(e:*)
				{
					color = obj.material.wireColor;
				}	
				
				obj.material = new PhongColorMaterial(color, {width: 2.0});
			}
		}
		
		private function resetCamera(event: Event) : void
		{	
			
			
			cam.distance = 800;
			cam.zoom = 60;
			
			camObject = view.scene.getChildByName("sphere1");
			cam.target = camObject;
			
			cam.moveTo(stareObject.x, stareObject.y +2000, stareObject.z);
			
			cam.hover();
			view.render();
		}
		
		
		private function changeZoom(event:MouseEvent): void
		{
			if(event.delta > 0){
				cam.zoom = cam.zoom + 20;
				
			} else {
				cam.zoom = cam.zoom - 20;
			}
			
		}
		
		
		private function MouseDown(event:MouseEvent):void
		{
			
			lastPanAngle = cam.panAngle;
			lastTiltAngle = cam.tiltAngle;
			lastMouseX = stage.mouseX;
			lastMouseY = stage.mouseY;
			move = true;			
			
		}
		
		private function MouseUp(event:MouseEvent):void
		{
			move = false;
		}
	}
}