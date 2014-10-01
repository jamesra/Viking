			// global variable(s);
			var svgObj;
			window.onload = checkSVGLoad;


			var checkSVGLoad = function(){
				svgObj = document.getElementById("svgObj") || document.getElementById("flashObj");
				if(svgObj){
					var svgDoc = fetchSVGDocument(svgObj);
					if(svgDoc && svgDoc.documentElement){
						updateGUIElements();
						return;
					}
				}
				// for some reason, we're not ready yet
				// THINK: give up after a while?
				// (i.e., don't pool for the SVG load state forever in case it's impossible: for example, no SVG plug-in and no flash support on IE)
				// (on the other hand, if we have really complex content to render, it might take longer to become available than a predefined timeout)
				setTimeout(checkSVGLoad, 200);
			}

			// setup initial call
			
			var updateGUIElements = function(){
				var svgDoc = fetchSVGDocument(svgObj);
				if(svgDoc){
					// check for availability of the required DOM APIs
					if(typeof(svgDoc.documentElement.currentScale) != "undefined"){
						setZoomInteractors(true);
						// at least one interactor is enabled
						setResetAll(true);
					}
					if(typeof(svgDoc.documentElement.currentTranslate) != "undefined"){
						setPanInteractors(true);
						// at least one interactor is enabled
						setResetAll(true);
					}
					if(typeof(svgDoc.documentElement.currentRotate) != "undefined"){
						setRotateInteractors(true);
						// at least one interactor is enabled
						setResetAll(true);
					}
				}
			};
			
			var setZoomInteractors = function(toEnable){
				setElements(["zoomin", "zoomout", "resetzoom"], toEnable);
			};
			var setPanInteractors = function(toEnable){
				setElements(["panleft", "panup", "panright", "pandown", "resetpan"], toEnable);
			};
			var setRotateInteractors = function(toEnable){
				setElements(["rotatecw", "rotateccw", "resetrotation"], toEnable);
			};
			var setResetAll = function(toEnable){
				setElements(["resetall"], toEnable);
			};
			var setElements = function(eleArray, toEnable){
				for(var i = 0; i < eleArray.length; i++){
					var ele = document.getElementById(eleArray[i]);
					if(ele){
						ele.disabled = !toEnable;
					}
				}
			};

			var fetchSVGDocument = function(containerElement){
				if(!containerElement){
					// no need to proceed if there's no container element
					return null;
				}
				// NOTE: weird behavior in Webkit: if one gets contentDocument while the SVG object is still being loaded, it seems that the SVG container document will actually hold an HTML document?!
				// (when used together with window and/or object onload events, as if the are being triggered before the SVG document is really ready, which feels like buggy...)
				// (it's like crawling though contentDocument exposes a unstable, still-being-parsed subtree)
				// TODO: find out whether this is an issue or a "feature"
				// NOTE: using getSVGDocument to fetch the document instead seems to workaround the issue
				try{
					// Rendering engines: Webkit (Safari, Chrome), Trident (Internet Explorer) with ASV and Renesis
					return containerElement.getSVGDocument();
				}catch(e){
					if(typeof(containerElement.contentDocument) != "undefined"){
						// Rendering engines: Gecko (Firefox), Presto (Opera), SVG Web
						// (Webkit (Safari, Chrome) is buggy regarding this, getSVGDocument seems more reliable)
						return containerElement.contentDocument;
					}
					return null;
				}
			};
			
			/**
			 * Zooms the document
			 * NOTE: due to (older) implementation issues, it's advised that it doesn't contain a large number of decimal cases.
			 * (see http://heldermagalhaes.com/stuff/svg/demos/ZoomAndPan-Demo.svg )
			 * 
			 * @param magnitude zoom multiplier: above 1 for zoom in, below 1 for zoom out.
			 * @param isAbsolute if the input value is to be considered relative or absolute: can be ommitted when relative.
			 */
			var zoom = function(magnitude, isAbsolute){
				// NOTE: we fetch the interface everytime as the document may change between interactions
				// (the SVG may contain links to other SVG documents, for example)
				var svgDoc = fetchSVGDocument(svgObj);
				if(svgDoc && svgDoc.documentElement){
					if(isAbsolute){
						svgDoc.documentElement.currentScale = magnitude;
					}else{
						svgDoc.documentElement.currentScale *= magnitude;
					}
				}else{
					alert("Error occurred while zooming!");
					setZoomInteractors(false);
				}
			};
			
			/**
			 * Pans the document
			 * 
			 * @param x horizontal coordinate shift: negative for left, positive for right.
			 * @param y vertical coordinate shift: negative for up, positive for down.
			 * @param isAbsolute if the input value is to be considered relative or absolute: can be ommitted when relative.
			 */
			var pan = function(x, y, isAbsolute){
				// NOTE: we fetch the interface every time as the document may change between interactions
				// (the SVG may contain links to other SVG documents, for example)
				var svgDoc = fetchSVGDocument(svgObj);
				if(svgDoc && svgDoc.documentElement){
					if(isAbsolute){
						if(typeof(svgDoc.documentElement.currentTranslate.setX) != "undefined"){
							// accomodate for SVG Web, which currently doesn't support the standard way to set the attribute
							// http://svgweb.googlecode.com/svn/trunk/docs/UserManual.html#known_issues25
							svgDoc.documentElement.currentTranslate.setX(x);
						}else{
							svgDoc.documentElement.currentTranslate.x = x;
						}
						if(typeof(svgDoc.documentElement.currentTranslate.setY) != "undefined"){
							// accomodate for SVG Web, which currently doesn't support the standard way to set the attribute
							// http://svgweb.googlecode.com/svn/trunk/docs/UserManual.html#known_issues25
							svgDoc.documentElement.currentTranslate.setY(y);
						}else{
							svgDoc.documentElement.currentTranslate.y = y;
						}
					}else{
						if(typeof(svgDoc.documentElement.currentTranslate.setX) != "undefined"){
							// accomodate for SVG Web, which currently doesn't support the standard way to set the attribute
							// http://svgweb.googlecode.com/svn/trunk/docs/UserManual.html#known_issues25
							svgDoc.documentElement.currentTranslate.setX(svgDoc.documentElement.currentTranslate.x + x);
						}else{
							svgDoc.documentElement.currentTranslate.x += x;
						}
						if(typeof(svgDoc.documentElement.currentTranslate.setY) != "undefined"){
							// accomodate for SVG Web, which currently doesn't support the standard way to set the attribute
							// http://svgweb.googlecode.com/svn/trunk/docs/UserManual.html#known_issues25
							svgDoc.documentElement.currentTranslate.setY(svgDoc.documentElement.currentTranslate.y + y);
						}else{
							svgDoc.documentElement.currentTranslate.y += y;
						}
					}
				}else{
					alert("Error occurred while panning!");
					setPanInteractors(false);
				}
			};

			/**
			 * Rotates the document
			 * NOTE: as this was introduced in SVG Tiny 1.2, it's (currently) little supported.
			 * 
			 * @param angle rotation angle, in degrees: positive for clockwise, negative for counter-clockwise
			 * @param isAbsolute if the input value is to be considered relative or absolute: can be ommitted when relative.
			 */
			var rotate = function(angle, isAbsolute){
				// NOTE: we fetch the interface everytime as the document may change between interactions
				// (the SVG may contain links to other SVG documents, for example)
				var svgDoc = fetchSVGDocument(svgObj);
				if(svgDoc && svgDoc.documentElement){
					if(isAbsolute){
						svgDoc.documentElement.currentRotate = angle;
					}else{
						svgDoc.documentElement.currentRotate += angle;
					}
				}else{
					alert("Error occurred while rotating!");
					setRotateInteractors(false);
				}
			};
	