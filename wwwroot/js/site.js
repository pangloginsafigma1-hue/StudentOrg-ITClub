document.querySelectorAll("[data-division-dropdown]").forEach((dropdown) => {
    const allInput = dropdown.querySelector('input[type="checkbox"][data-all-divisions]');
    const divisionInputs = [...dropdown.querySelectorAll('input[type="checkbox"][name]')];
    const label = dropdown.querySelector("[data-division-label]");

    const updateLabel = () => {
        const selected = divisionInputs
            .filter((input) => input.checked)
            .map((input) => input.closest("label")?.querySelector("span")?.textContent?.trim())
            .filter(Boolean);
        const allSelected = divisionInputs.length > 0 && selected.length === divisionInputs.length;
        if (allInput instanceof HTMLInputElement) {
            allInput.checked = allSelected;
        }
        if (label) {
            label.textContent = allSelected
                ? "All Divisions"
                : selected.length === 0
                    ? "No divisions selected"
                    : selected.join(", ");
        }
    };

    allInput?.addEventListener("change", () => {
        divisionInputs.forEach((input) => input.checked = allInput.checked);
        updateLabel();
    });
    divisionInputs.forEach((input) => input.addEventListener("change", updateLabel));
    updateLabel();
});

document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    button.addEventListener("click", () => {
        const password = document.querySelector("#passwordInput");
        if (!(password instanceof HTMLInputElement)) {
            return;
        }

        const isVisible = password.type === "text";
        password.type = isVisible ? "password" : "text";
        button.setAttribute("aria-pressed", String(!isVisible));
        button.setAttribute("aria-label", isVisible ? "Show password" : "Hide password");
    });
});

document.querySelectorAll("[data-member-status-autosave]").forEach((select) => {
    if (select instanceof HTMLSelectElement) {
        select.addEventListener("change", () => select.form?.requestSubmit());
    }
});

document.querySelectorAll("[data-portal-transition]").forEach((link) => {
    link.addEventListener("click", (event) => {
        if (!(event instanceof MouseEvent)
            || event.button !== 0
            || event.metaKey
            || event.ctrlKey
            || event.shiftKey
            || event.altKey
            || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            return;
        }

        event.preventDefault();
        document.body.classList.add("auth-transition-out");
        window.setTimeout(() => {
            window.location.assign(link.href);
        }, 160);
    });
});

document.querySelectorAll("[data-student-id='true']").forEach((input) => {
    input.addEventListener("input", () => {
        if (input instanceof HTMLInputElement) {
            input.value = input.value.replace(/[^\d-]/g, "");
        }
    });
});

const deleteMemberModal = document.querySelector("#deleteMemberModal");
if (deleteMemberModal instanceof HTMLElement) {
    deleteMemberModal.addEventListener("show.bs.modal", (event) => {
        if (!(event instanceof Event) || !(event.relatedTarget instanceof HTMLElement)) {
            return;
        }

        const memberId = event.relatedTarget.dataset.memberId;
        const memberName = event.relatedTarget.dataset.memberName;
        const idInput = deleteMemberModal.querySelector("#deleteMemberId");
        const message = deleteMemberModal.querySelector("#deleteMemberMessage");

        if (idInput instanceof HTMLInputElement && memberId) {
            idInput.value = memberId;
        }

        if (message && memberName && memberId) {
            message.textContent = `Delete ${memberName} (${memberId})?`;
        }
    });
}

const activityModal = document.querySelector("#activityModal");
if (activityModal instanceof HTMLElement) {
    activityModal.addEventListener("show.bs.modal", (event) => {
        if (!(event instanceof Event) || !(event.relatedTarget instanceof HTMLElement)) {
            return;
        }

        const trigger = event.relatedTarget;
        const form = activityModal.querySelector("form");
        const id = activityModal.querySelector("#activityId");
        const name = activityModal.querySelector("#activityName");
        const location = activityModal.querySelector("#activityLocation");
        const date = activityModal.querySelector("#activityDate");
        const endDate = activityModal.querySelector("#activityEndDate");
        const status = activityModal.querySelector("#activityStatus");
        const description = activityModal.querySelector("#activityDescription");
        const division = activityModal.querySelector("#activityDivision");
        const title = activityModal.querySelector("#activityModalTitle");
        const submit = activityModal.querySelector("#saveActivityButton");
        const activityForm = activityModal.querySelector("form");

        if (!(form instanceof HTMLFormElement)
            || !(activityForm instanceof HTMLFormElement)
            || !(id instanceof HTMLInputElement)
            || !(name instanceof HTMLInputElement)
            || !(location instanceof HTMLInputElement)
            || !(date instanceof HTMLInputElement)
            || !(endDate instanceof HTMLInputElement)
            || !(status instanceof HTMLSelectElement)
            || !(description instanceof HTMLTextAreaElement)
            || (division !== null
                && !(division instanceof HTMLInputElement || division instanceof HTMLSelectElement))
            || !title
            || !submit) {
            return;
        }

        form.reset();
        const isEdit = trigger.hasAttribute("data-activity-edit");
        id.value = isEdit ? trigger.dataset.activityId ?? "" : "0";
        name.value = isEdit ? trigger.dataset.activityName ?? "" : "";
        location.value = isEdit ? trigger.dataset.activityLocation ?? "" : "";
        description.value = isEdit ? trigger.dataset.activityDescription ?? "" : "";

        const currentDate = new Date();
        const today = new Date(currentDate.getTime() - currentDate.getTimezoneOffset() * 60_000)
            .toISOString()
            .slice(0, 10);
        date.value = isEdit ? trigger.dataset.activityDate ?? `${today}T09:00` : `${today}T09:00`;
        endDate.value = isEdit ? trigger.dataset.activityEndDate ?? `${today}T10:00` : `${today}T10:00`;
        endDate.min = date.value;
        status.value = isEdit ? trigger.dataset.activityStatus ?? "Upcoming" : "Upcoming";
        if (division instanceof HTMLInputElement || division instanceof HTMLSelectElement) {
            division.value = isEdit ? trigger.dataset.activityDivisionId ?? "" : "";
        }
        const activityDivisionInputs = document.querySelectorAll("input[name='Form.DivisionIds']");
        const hasAssignedDivisions = Boolean(
            trigger.dataset.activityDivisionId || trigger.dataset.activityDivisionId2
        );
        activityDivisionInputs.forEach((input) => {
            if (input instanceof HTMLInputElement) {
                input.checked = !isEdit || !hasAssignedDivisions
                    || input.value === (trigger.dataset.activityDivisionId ?? "")
                    || input.value === (trigger.dataset.activityDivisionId2 ?? "");
            }
        });
        const firstDivisionInput = activityDivisionInputs[0];
        if (firstDivisionInput instanceof HTMLInputElement) {
            firstDivisionInput.dispatchEvent(new Event("change", { bubbles: true }));
        }

        title.textContent = isEdit ? "Edit Activity" : "Add Activity";
        submit.textContent = isEdit ? "Save Changes" : "Save Activity";
    });

    if (activityModal.dataset.openModal === "true" && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(activityModal).show();
    }
}

const adminTermModal = document.querySelector("#adminTermModal");
if (adminTermModal instanceof HTMLElement) {
    adminTermModal.addEventListener("show.bs.modal", (event) => {
        if (!(event instanceof Event) || !(event.relatedTarget instanceof HTMLElement)) {
            return;
        }

        const trigger = event.relatedTarget;
        const adminId = adminTermModal.querySelector("#adminTermId");
        const position = adminTermModal.querySelector("#adminTermPosition");
        const start = adminTermModal.querySelector("#adminTermStart");
        const description = adminTermModal.querySelector("#adminTermModalDescription");
        if (!(adminId instanceof HTMLInputElement)
            || !(position instanceof HTMLInputElement)
            || !(start instanceof HTMLInputElement)
            || !(description instanceof HTMLElement)) {
            return;
        }

        adminId.value = trigger.dataset.adminTermId ?? "";
        position.value = trigger.dataset.adminTermPosition ?? "";
        start.value = new Date().toISOString().slice(0, 10);
        description.textContent = `Record a position term for ${trigger.dataset.adminTermName ?? "this administrator"}.`;
    });
}

const activityForm = document.querySelector("#activityModal form");
const activityStart = document.querySelector("#activityDate");
const activityEnd = document.querySelector("#activityEndDate");
if (activityForm instanceof HTMLFormElement
    && activityStart instanceof HTMLInputElement
    && activityEnd instanceof HTMLInputElement) {
    const syncActivityEnd = () => {
        activityEnd.min = activityStart.value;
        if (activityEnd.value && activityStart.value && activityEnd.value <= activityStart.value) {
            activityEnd.setCustomValidity("The end date and time must be after the start.");
        } else {
            activityEnd.setCustomValidity("");
        }
    };
    activityStart.addEventListener("input", syncActivityEnd);
    activityEnd.addEventListener("input", syncActivityEnd);
    activityForm.addEventListener("submit", (event) => {
        syncActivityEnd();
        if (!activityForm.checkValidity()) {
            event.preventDefault();
            activityForm.reportValidity();
        }
    });
}

const adminRoleSelect = document.querySelector("[data-admin-role]");
const adminDivisionField = document.querySelector("[data-admin-division-field]");
if (adminRoleSelect instanceof HTMLSelectElement && adminDivisionField instanceof HTMLElement) {
    const updateDivisionVisibility = () => {
        const role = adminRoleSelect.value;
        const isSuperAdmin = role === "SuperAdmin";
        const isDivisionAdmin = role === "DivisionAdmin";
        const isMember = role === "Member";
        adminDivisionField.hidden = !isDivisionAdmin;
        const enabledField = document.querySelector("[data-admin-enabled-field]");
        if (enabledField instanceof HTMLElement) {
            enabledField.hidden = isMember;
        }
        const passwordFields = document.querySelector("[data-admin-password-fields]");
        if (passwordFields instanceof HTMLElement) {
            passwordFields.hidden = isMember;
        }
        const memberWarning = document.querySelector("[data-admin-member-warning]");
        if (memberWarning instanceof HTMLElement) {
            memberWarning.hidden = !isMember;
        }
        const saveButton = document.querySelector("[data-admin-save-button]");
        if (saveButton instanceof HTMLButtonElement) {
            saveButton.textContent = isMember ? "Save as Member" : "Save Account";
        }
        const positionLabel = document.querySelector("[data-admin-position-label]");
        if (positionLabel instanceof HTMLElement) {
            positionLabel.textContent = isSuperAdmin
                ? "President"
                : isMember
                    ? "Member"
                    : "Division Head";
        }
    };
    adminRoleSelect.addEventListener("change", updateDivisionVisibility);
    updateDivisionVisibility();
}

const adminAccountForm = document.querySelector("[data-admin-account-form]");
if (adminAccountForm instanceof HTMLFormElement) {
    let memberConversionConfirmed = false;
    const memberConfirmModalElement = document.querySelector("#adminMemberConfirmModal");
    const memberConfirmButton = memberConfirmModalElement?.querySelector("[data-admin-member-confirm]");
    const memberConfirmModal = memberConfirmModalElement instanceof HTMLElement
        && window.bootstrap?.Modal
        ? window.bootstrap.Modal.getOrCreateInstance(memberConfirmModalElement)
        : null;

    adminAccountForm.addEventListener("submit", (event) => {
        const role = adminRoleSelect instanceof HTMLSelectElement
            ? adminRoleSelect.value
            : "";
        if (role === "Member" && !memberConversionConfirmed) {
            event.preventDefault();
            if (memberConfirmModal) {
                memberConfirmModal.show();
            }
        }
    });

    if (memberConfirmButton instanceof HTMLButtonElement) {
        memberConfirmButton.addEventListener("click", () => {
            memberConversionConfirmed = true;
            memberConfirmModal?.hide();
            adminAccountForm.requestSubmit();
        });
    }
}

const deleteActivityModal = document.querySelector("#deleteActivityModal");
if (deleteActivityModal instanceof HTMLElement) {
    deleteActivityModal.addEventListener("show.bs.modal", (event) => {
        if (!(event instanceof Event) || !(event.relatedTarget instanceof HTMLElement)) {
            return;
        }

        const trigger = event.relatedTarget;
        const id = deleteActivityModal.querySelector("#deleteActivityId");
        const message = deleteActivityModal.querySelector("#deleteActivityMessage");
        const warning = deleteActivityModal.querySelector("#deleteActivityAttendanceWarning");

        if (id instanceof HTMLInputElement) {
            id.value = trigger.dataset.activityId ?? "";
        }

        if (message) {
            message.textContent = `Delete "${trigger.dataset.activityName ?? "this activity"}"?`;
        }

        if (warning instanceof HTMLElement) {
            warning.hidden = trigger.dataset.attendanceRecorded !== "True";
        }
    });
}

const attendanceRoster = document.querySelector("#attendanceRoster");

if (attendanceRoster instanceof HTMLElement) {
    const checkboxes = Array.from(
        attendanceRoster.querySelectorAll(".attendance-member-checkbox")
    ).filter((checkbox) => checkbox instanceof HTMLInputElement);
    const presentCount = document.querySelector("#presentCount");
    const absentCount = document.querySelector("#absentCount");

    const updateAttendanceSummary = () => {
        const present = checkboxes.filter((checkbox) => checkbox.checked).length;
        if (presentCount) {
            presentCount.textContent = String(present);
        }

        if (absentCount) {
            absentCount.textContent = String(checkboxes.length - present);
        }

        checkboxes.forEach((checkbox) => {
            const row = checkbox.closest("[data-attendance-row]");
            const state = row?.querySelector("[data-attendance-state]");
            if (state instanceof HTMLElement) {
                state.textContent = checkbox.checked ? "Present" : "Absent";
                state.classList.toggle("is-present", checkbox.checked);
                state.classList.toggle("is-absent", !checkbox.checked);
            }
        });
    };

    checkboxes.forEach((checkbox) => {
        checkbox.addEventListener("change", updateAttendanceSummary);
    });

    document.querySelectorAll("[data-attendance-set-all]").forEach((button) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        button.addEventListener("click", () => {
            const markPresent = button.dataset.attendanceSetAll === "present";
            checkboxes.forEach((checkbox) => {
                checkbox.checked = markPresent;
            });
            updateAttendanceSummary();
        });
    });

    const searchInput = document.querySelector("#attendanceSearch");
    const noResults = document.querySelector("[data-attendance-no-results]");
    const searchButton = document.querySelector("[data-attendance-search]");
    const applySearch = () => {
        const searchTerm = searchInput instanceof HTMLInputElement
            ? searchInput.value.trim().toLocaleLowerCase()
            : "";
        let visibleCount = 0;

        attendanceRoster.querySelectorAll("[data-attendance-row]").forEach((row) => {
            if (!(row instanceof HTMLElement)) {
                return;
            }

            const isMatch = (row.dataset.memberSearch ?? "")
                .toLocaleLowerCase()
                .includes(searchTerm);
            row.hidden = !isMatch;
            if (isMatch) {
                visibleCount += 1;
            }
        });

        if (noResults instanceof HTMLElement) {
            noResults.hidden = visibleCount > 0;
        }
    };

    if (searchInput instanceof HTMLInputElement) {
        searchInput.addEventListener("input", applySearch);
        searchInput.addEventListener("keydown", (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                applySearch();
            }
        });
    }

    if (searchButton instanceof HTMLButtonElement) {
        searchButton.addEventListener("click", applySearch);
    }

    updateAttendanceSummary();
}

const settingsPhotoInput = document.querySelector("#Photo");
const settingsPhotoPreview = document.querySelector(".settings-photo");
if (settingsPhotoInput instanceof HTMLInputElement && settingsPhotoPreview instanceof HTMLImageElement) {
    settingsPhotoInput.addEventListener("change", () => {
        const file = settingsPhotoInput.files?.[0];
        if (!file) {
            return;
        }

        settingsPhotoPreview.src = URL.createObjectURL(file);
    });
}
