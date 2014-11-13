 $.fn.qtip.styles.mystyle = { // Last part is the name of the style
        border: {
            width: 5,
            radius: 10,
           
        },        
        name: 'green',
        padding: 7,
        textAlign: 'center',
        tip: true
    };

     $.fn.qtip.styles.hoverStyle = { // Last part is the name of the style
        border: {
            width: 5,
            radius: 10,   
                 
        },        
        name: 'green', 
        padding: 3,
        textAlign: 'center',
        tip: true
    };



 $(document).ready(function() {

$("#Structure").qtip({
            content: { text: ' Try 3D Structure Visualization' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });         

        
    $("#Network").qtip({
            content: { text: 'Generate 3D network Diagram for a cell' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });

    $("#Motifs").qtip({
            content: { text: 'Generate network for a class of cells' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });   

        $("#Stats").qtip({
            content: { text: 'Analyze Cell Statistics' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });   

        $("#VikingPlot").qtip({
            content: { text: 'View 3D morphology of a Cell' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });   

        $("#manageAccount").qtip({
            content: { text: 'Manage your Account' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });  

        $("#contactDeveloper").qtip({
            content: { text: "Visit Developer's webpage" },
            position: {
                corner: {
                    tooltip: 'topMiddle', // Use the corner.
                    target: 'bottomMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });  

        $("#twitterLink").qtip({
            content: { text: 'Follow MarcLab on Twitter' },
            position: {
                corner: {
                    tooltip: 'bottomMiddle', // Use the corner.
                    target: 'topMiddle' // ...and opposite corner
                }
            },
            solo: false,
            delay: 0,
            style: 'hoverStyle',
            show: 'mouseover',
            hide: 'mouseout'

        });  

        



});
   
