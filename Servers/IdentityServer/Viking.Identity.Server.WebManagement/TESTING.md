# UI Refactor Testing Guide

## Overview

This document provides testing guidelines for the Viking Identity Server UI refactor, focusing on Bootstrap 5 migration, new features, and responsive design.

## Pre-Testing Checklist

- [ ] Clear browser cache and cookies
- [ ] Test in multiple browsers (Chrome, Firefox, Edge, Safari)
- [ ] Test on different screen sizes (mobile, tablet, desktop)
- [ ] Ensure database has test data (organizations, volumes, users, groups)

## Functional Testing

### 1. Dashboard (Home/Index)

**Test Cases:**
- [ ] Dashboard loads with correct statistics
- [ ] User's organizations are displayed correctly
- [ ] User's accessible volumes are shown
- [ ] Quick action buttons work
- [ ] Statistics cards display correct counts
- [ ] Links navigate to correct pages

**Responsive Testing:**
- [ ] Cards stack properly on mobile
- [ ] Text remains readable on small screens
- [ ] Buttons are appropriately sized for touch

### 2. Navigation & Layout

**Test Cases:**
- [ ] Navigation menu expands/collapses correctly
- [ ] Dropdown menus work properly
- [ ] Breadcrumbs display on all pages
- [ ] Breadcrumb links navigate correctly
- [ ] Logo links to dashboard
- [ ] Login/logout functionality works
- [ ] User dropdown shows correct options

**Responsive Testing:**
- [ ] Mobile menu toggle works
- [ ] Navigation is accessible on small screens
- [ ] Dropdowns don't overflow screen

### 3. Organizations

**Index Page:**
- [ ] DataTable search works
- [ ] DataTable sorting works
- [ ] DataTable pagination works
- [ ] Parent-child relationships display correctly
- [ ] "Create New" button works
- [ ] Action buttons work (Details, Edit, Delete)

**Details Page:**
- [ ] All tabs load correctly
- [ ] Overview tab shows correct information
- [ ] Volumes tab lists volumes correctly
- [ ] Child Organizations tab lists children
- [ ] Permissions tab displays permissions
- [ ] Tab switching works smoothly

**Create/Edit Pages:**
- [ ] Form validation works
- [ ] Dropdowns populate correctly
- [ ] Save button works
- [ ] Cancel button navigates back
- [ ] Success message displays after save
- [ ] Loading spinner shows during submission

**Delete Page:**
- [ ] Warning message displays
- [ ] Confirmation dialog appears
- [ ] Delete action works
- [ ] Cancel prevents deletion

### 4. Volumes

**Index Page:**
- [ ] Card view and table view toggle works
- [ ] Bulk selection checkboxes appear (admin only)
- [ ] Bulk edit button appears when volumes selected
- [ ] Bulk edit button shows correct count
- [ ] "Select All" works in table view
- [ ] Volumes grouped by organization correctly
- [ ] DataTable works in table view
- [ ] Search/filter works

**Bulk Operations:**
- [ ] Selecting volumes shows bulk button
- [ ] Bulk edit page loads with selected volumes
- [ ] Permission grid displays correctly
- [ ] Changes apply to all selected volumes
- [ ] Success message shows count of updated volumes

**Details Page:**
- [ ] Volume information displays correctly
- [ ] Endpoint link works
- [ ] Permission management button works
- [ ] Parent organization link works

### 5. Users

**Index Page:**
- [ ] User table displays correctly
- [ ] DataTable search works
- [ ] Status badges show correctly
- [ ] Action buttons work
- [ ] Email confirmation status displays

**Details Page:**
- [ ] All tabs load correctly
- [ ] Profile tab shows user information
- [ ] Groups tab lists user groups
- [ ] Permissions tab shows accessible volumes
- [ ] Organizations tab displays correctly

**Edit Organizations Page:**
- [ ] Search functionality works
- [ ] Select All/Deselect All work
- [ ] Show Selected Only toggle works
- [ ] Selection counter updates correctly
- [ ] Save applies changes correctly

### 6. Groups

**Index Page:**
- [ ] Group table displays correctly
- [ ] Member counts show correctly
- [ ] DataTable works
- [ ] Action buttons work

**Details Page:**
- [ ] Tabbed interface works
- [ ] Member lists display correctly
- [ ] Permission information shows
- [ ] Subgroups display correctly

**Create/Edit Pages:**
- [ ] Membership checklists work
- [ ] User and group selection works
- [ ] Form saves correctly

### 7. Permissions

**Edit Permissions:**
- [ ] Permission grid displays correctly
- [ ] Checkboxes update state correctly
- [ ] Group and user sections work
- [ ] Save applies changes
- [ ] Success toast appears
- [ ] Visual feedback on checkbox changes

**Bulk Edit Permissions:**
- [ ] Selected volumes list displays
- [ ] Permission grid shows all users/groups
- [ ] Changes apply to all volumes
- [ ] Warning messages display
- [ ] Confirmation dialog works

### 8. My Rights Page

- [ ] Volumes grouped by organization
- [ ] Search functionality works
- [ ] Filter buttons work (All, Read, Annotate, Review)
- [ ] Volume cards display correctly
- [ ] Permission badges show correctly
- [ ] Access Volume buttons work

## Responsive Design Testing

### Mobile (< 768px)

**Test on actual device or browser dev tools:**

- [ ] Navigation collapses to hamburger menu
- [ ] Tables become scrollable or stack
- [ ] Cards stack vertically
- [ ] Forms are readable and usable
- [ ] Buttons are appropriately sized for touch
- [ ] Modals fit on screen
- [ ] No horizontal scrolling

### Tablet (768px - 1024px)

- [ ] Layout adapts appropriately
- [ ] Cards use appropriate column spans
- [ ] Tables remain usable
- [ ] Navigation works well

### Desktop (> 1024px)

- [ ] Full layout displays correctly
- [ ] No unnecessary whitespace
- [ ] Hover effects work
- [ ] Dropdowns position correctly

## Browser Compatibility

Test in:
- [ ] Chrome (latest)
- [ ] Firefox (latest)
- [ ] Edge (latest)
- [ ] Safari (latest, macOS/iOS)

**Check for:**
- [ ] Consistent styling
- [ ] JavaScript functionality
- [ ] Form submissions
- [ ] DataTable functionality
- [ ] Bootstrap components

## Accessibility Testing

### Keyboard Navigation

- [ ] All interactive elements accessible via Tab
- [ ] Focus indicators visible
- [ ] Dropdowns open with Enter/Space
- [ ] Forms submit with Enter
- [ ] Escape closes modals/dropdowns
- [ ] No keyboard traps

### Screen Readers

Test with NVDA (Windows) or VoiceOver (macOS):

- [ ] Page structure announced correctly
- [ ] Form labels announced
- [ ] Button purposes clear
- [ ] Error messages announced
- [ ] Success messages announced
- [ ] Table headers announced

### Visual

- [ ] Color contrast meets WCAG AA standards
- [ ] Focus indicators are visible
- [ ] Icons have text alternatives
- [ ] Text is readable at all sizes
- [ ] No information conveyed by color alone

## Performance Testing

- [ ] Pages load within 2 seconds
- [ ] DataTables initialize quickly
- [ ] Form submissions are responsive
- [ ] No JavaScript errors in console
- [ ] No unnecessary network requests

## Error Handling

- [ ] Validation errors display correctly
- [ ] Server errors show user-friendly messages
- [ ] Network errors handled gracefully
- [ ] 404 pages work correctly
- [ ] Unauthorized access handled correctly

## Edge Cases

- [ ] Empty lists display appropriate messages
- [ ] Long names/text wrap correctly
- [ ] Very large datasets handled (pagination)
- [ ] Concurrent edits handled appropriately
- [ ] Session timeout handled gracefully

## Regression Testing

Verify existing functionality still works:

- [ ] User authentication/authorization
- [ ] Permission checks
- [ ] Data persistence
- [ ] Existing workflows
- [ ] Integration with other systems

## Test Data Requirements

Create test data for:

- Multiple organizations with hierarchies
- Volumes in different organizations
- Users with various permission levels
- Groups with members
- Users with no permissions
- Empty organizations/volumes
- Long names and descriptions

## Known Issues

Document any issues found during testing:

1. Issue description
2. Steps to reproduce
3. Expected behavior
4. Actual behavior
5. Browser/device information
6. Severity (Critical, High, Medium, Low)

## Sign-off

- [ ] All critical test cases passed
- [ ] All high-priority test cases passed
- [ ] Responsive design verified
- [ ] Accessibility baseline met
- [ ] Performance acceptable
- [ ] Ready for user acceptance testing


