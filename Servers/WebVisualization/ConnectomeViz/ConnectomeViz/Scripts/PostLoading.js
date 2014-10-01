var root = window.location;

var re = new RegExp('^(?:f|ht)tp(?:s)?\://(.*?)/(.*?)/', 'im');
var mat = root.toString().match(re)[0];

var Url = mat;


function updateColors(actualColor, changed, elementType)
{
   
   var oldColor = "";

   

    if (elementType == "Edge")
    {
        oldColor = edgeColorLookUp[actualColor];
        if(oldColor ==  undefined)
        {
            actualColor = '#'+ actualColor;
            oldColor = edgeColorLookUp[actualColor];
        }
        edgeColorLookUp[actualColor] = changed;
    }
    else
    {
        
        oldColor= nodeColorLookUp[actualColor];
        if(oldColor ==  undefined)
        {
            actualColor = '#'+ actualColor;
            oldColor = nodeColorLookUp[actualColor];
        }
        nodeColorLookUp[actualColor] = changed;

     }

     try{

        var tempButtonReference = document.getElementById(actualColor+ elementType + "Button");       
        
        tempButtonReference.style.backgroundColor = changed;   
    }
    catch(err){
     }
     
    
    svgDocument = document.getElementById("SVGGraph").contentDocument;
    var changedRoot = svgDocument.documentElement;
    var newChildren = changedRoot.getElementsByTagName('g');
    var newLen = newChildren.length;

    for (var i = 1; i < newLen; i++) {

        var tempElement;
        var tempColor;

        if (elementType === "Node" && newChildren.item(i).getAttribute("class") == "node") {
            try {
               tempColor = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");
               if (tempColor == oldColor)
                newChildren.item(i).getElementsByTagName("polygon")[0].setAttribute("fill",changed);
            }
            catch (err) {
                 tempColor = newChildren.item(i).getElementsByTagName("ellipse")[0].getAttribute("fill");
               if (tempColor == oldColor)
                newChildren.item(i).getElementsByTagName("ellipse")[0].setAttribute("fill",changed);
            
            }

           

            
        }

        else if (elementType ==="Edge" && newChildren.item(i).getAttribute("class") == "edge") {


//            try {
                tempColor = newChildren.item(i).getElementsByTagName("path")[0].getAttribute("stroke");
              
                if (tempColor == oldColor)
                {
                    newChildren.item(i).getElementsByTagName("path")[0].setAttribute("stroke",changed);

                    var polys = newChildren.item(i).getElementsByTagName("polygon");
                    for(var i = 0; i < polys.length; i++)
                    {
                        polys[i].setAttribute("stroke",changed);
                        if(polys[i].getAttribute("fill") != 'none')
                            polys[i].setAttribute("fill",changed);
                    }

                    var polyLines = newChildren.item(i).getElementsByTagName("polyline");

                    for(var i = 0; i < polyLines.length; i++)
                    {
                        polyLines[i].setAttribute("stroke",changed);
                        polyLines[i].setAttribute("fill",changed);
                    }

                }
//            }
//            catch (err) {
//                tempElement = newChildren.item(i).getElementsByTagName("polygon")[0].getAttribute("fill");
//         
//                if (tempColor == oldColor)
//                {
//                    newChildren.item(i).getElementsByTagName("polygon")[0].setAttribute("fill",changed);
//                }
//                
//            }
           
        }
       }

}

function junk (elementType) {

        
       var activeColor = "";
        

//        nodeColorTable["#00cd00"] = "GAC"; // Green
//        nodeColorTable["#8b6914"] = "unnamed";
//        nodeColorTable["#008b00"] = "CBab"; //Dark Green
//        nodeColorTable["#cd0000"] = "YAC"; // Red
//        nodeColorTable["#cdcd00"] = "Aii";  // Golden
//        nodeColorTable["#ee0000"] = "AC";   // Flag Red
//        nodeColorTable["blue"] = "BC | OFF BC | CBa"; //blue
//        nodeColorTable["purple"] = "ROD BC"; //purple
//        nodeColorTable["cadetblue"] = "GBC | CBb"; //cadetblue
//        nodeColorTable["grey"] = "TH | PROCESS | others"; //misc
//        nodeColorTable["saddlebrown"] = "GC"; //brown
//        nodeColorTable["white"] = "Ghost Nodes";

       $('#00cd00SpanNode').jPicker(
        {

            title: 'Pick a color',

          
          window:
          {
              expandable: true
          },

           position:
            {
              x: $(this).offset.left, // acceptable values "left", "center", "right", "screenCenter", or relative px value
              y: ($(this).offset.top - $(window).scrollTop()) + $(this).height() // acceptable values "top", "bottom", "center", or relative px value
            },
           color:
          {
   
            active: new $.jPicker.Color({ hex: '#00cd00' }), // accepts any declared jPicker.Color object or hex string WITH OR WITHOUT '#'
 
            },
         images:
          {
            clientPath: Url + 'Content/images/'
            }     

        },        
         function(color, context)
        {
          
          var hex = color.val('hex');

          updateColors('#00cd00', hex && '#' + hex || 'transparent', 'Node');

            
        }
      );  

                    
         $('#purpleSpanNode').jPicker(
        {

            title: 'Pick a color',

          
          window:
          {
              expandable: true
          },

           position:
            {
              x: $(this).offset.left, // acceptable values "left", "center", "right", "screenCenter", or relative px value
              y: ($(this).offset.top - $(window).scrollTop()) + $(this).height() // acceptable values "top", "bottom", "center", or relative px value
            },
           color:
          {
   
            active: new $.jPicker.Color({ hex: colourNameToHex('purple') }), // accepts any declared jPicker.Color object or hex string WITH OR WITHOUT '#'
 
            },
         images:
          {
            clientPath: Url + 'Content/images/'
            }     

        },        
         function(color, context)
        {
          
          var hex = color.val('hex');

          updateColors('purple', hex && '#' + hex || 'transparent', 'Node');

            
        }
      );  

      

        
        
      } 


function updateColorPanels(elementType)
{

    var elementTypeTable;

    var tempId = "";

    if(elementType == "Node")
        elementTypeTable = nodeTypeCount;
    else
        elementTypeTable = edgeTypeCount;
   

    for(key in elementTypeTable)
    {
              
        var tempColor = key;
        var append = "";

        if(key.indexOf('#') !=-1)
        {
            tempId = key.substring(1,key.length) + "Span" + elementType;
            append="#";
            
         }
        else
        {
            tempId = key + "Span" + elementType;
            tempColor = colourNameToHex(key);
        }

              

        $('#'+tempId).jPicker(
        {

            title: 'Pick a color',

          
          window:
          {
              expandable: true
          },

          position: { x: $(this).offset.left + $(this).width(), y: ($(this).offset.top - $(window).scrollTop()) + $(this).height() },

           color:
          {
   
            active: new $.jPicker.Color({ hex: tempColor }), // accepts any declared jPicker.Color object or hex string WITH OR WITHOUT '#'
 
            },
         images:
          {
            clientPath: Url + 'Content/images/'
            }     

        },        
         function(color, context)
        {
          
          var hex = color.val('hex');

          updateColors( $(this).attr('id').split('Span'+elementType)[0] , hex && '#' + hex || 'transparent', elementType);            
        }
//        ,
//         function(color, context)
//        {
//              
//          var hex = color.val('hex');

//          updateColors( "#cd0000", hex && '#' + hex || 'transparent', elementType);            
//        },

//        function(color, context)
//        {
//          
//        }

      ); 
        
    }
    
}

