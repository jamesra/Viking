// Write your JavaScript code.

// Toast Notification Utility
(function() {
    'use strict';
    
    window.VikingUI = window.VikingUI || {};
    
    // Create toast container if it doesn't exist
    if (!document.getElementById('toast-container')) {
        var toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
        toastContainer.setAttribute('style', 'z-index: 9999;');
        document.body.appendChild(toastContainer);
    }
    
    window.VikingUI.showToast = function(message, type) {
        type = type || 'info';
        var bgClass = {
            'success': 'bg-success',
            'error': 'bg-danger',
            'warning': 'bg-warning',
            'info': 'bg-info'
        }[type] || 'bg-info';
        
        var icon = {
            'success': 'bi-check-circle',
            'error': 'bi-x-circle',
            'warning': 'bi-exclamation-triangle',
            'info': 'bi-info-circle'
        }[type] || 'bi-info-circle';
        
        var toastId = 'toast-' + Date.now();
        var toastHtml = `
            <div id="${toastId}" class="toast ${bgClass} text-white" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="toast-header ${bgClass} text-white border-0">
                    <i class="bi ${icon} me-2"></i>
                    <strong class="me-auto">Notification</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;
        
        var toastContainer = document.getElementById('toast-container');
        toastContainer.insertAdjacentHTML('beforeend', toastHtml);
        
        var toastElement = document.getElementById(toastId);
        var toast = new bootstrap.Toast(toastElement, {
            autohide: true,
            delay: 5000
        });
        toast.show();
        
        // Remove toast element after it's hidden
        toastElement.addEventListener('hidden.bs.toast', function() {
            toastElement.remove();
        });
    };
    
    // Loading spinner utility
    window.VikingUI.showLoading = function(target) {
        target = target || document.body;
        var spinner = document.createElement('div');
        spinner.className = 'spinner-overlay';
        spinner.id = 'global-spinner';
        spinner.innerHTML = '<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading...</span></div>';
        target.appendChild(spinner);
    };
    
    window.VikingUI.hideLoading = function() {
        var spinner = document.getElementById('global-spinner');
        if (spinner) {
            spinner.remove();
        }
    };
    
    // Form submission loading state
    document.addEventListener('submit', function(e) {
        var form = e.target;
        if (form.tagName === 'FORM' && !form.dataset.noLoading) {
            var submitButton = form.querySelector('button[type="submit"], input[type="submit"]');
            if (submitButton) {
                submitButton.disabled = true;
                var originalText = submitButton.innerHTML;
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Processing...';
                submitButton.dataset.originalText = originalText;
            }
        }
    });
    
    // Show success message from TempData if available
    if (typeof TempDataSuccessMessage !== 'undefined' && TempDataSuccessMessage) {
        window.VikingUI.showToast(TempDataSuccessMessage, 'success');
    }
})();