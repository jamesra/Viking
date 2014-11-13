$(document).ready(function() {
    $('tbody tr:odd').each(function() {
        $(this).addClass('alternate');
    });
    $('tbody tr').bind('mouseover mouseout', function() { $(this).toggleClass('focus'); })
});