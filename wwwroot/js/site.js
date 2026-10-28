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
        const status = activityModal.querySelector("#activityStatus");
        const description = activityModal.querySelector("#activityDescription");
        const division = activityModal.querySelector("#activityDivision");
        const title = activityModal.querySelector("#activityModalTitle");
        const submit = activityModal.querySelector("#saveActivityButton");

        if (!(form instanceof HTMLFormElement)
            || !(id instanceof HTMLInputElement)
            || !(name instanceof HTMLInputElement)
            || !(location instanceof HTMLInputElement)
            || !(date instanceof HTMLInputElement)
            || !(status instanceof HTMLSelectElement)
            || !(description instanceof HTMLTextAreaElement)
            || !(division instanceof HTMLInputElement || division instanceof HTMLSelectElement)
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
        date.value = isEdit ? trigger.dataset.activityDate ?? today : today;
        status.value = isEdit ? trigger.dataset.activityStatus ?? "Upcoming" : "Upcoming";
        division.value = isEdit ? trigger.dataset.activityDivisionId ?? "" : "";

        title.textContent = isEdit ? "Edit Activity" : "Add Activity";
        submit.textContent = isEdit ? "Save Changes" : "Save Activity";
    });

    if (activityModal.dataset.openModal === "true" && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(activityModal).show();
    }
}

const adminRoleSelect = document.querySelector("[data-admin-role]");
const adminDivisionField = document.querySelector("[data-admin-division-field]");
if (adminRoleSelect instanceof HTMLSelectElement && adminDivisionField instanceof HTMLElement) {
    const updateDivisionVisibility = () => {
        adminDivisionField.hidden = adminRoleSelect.value !== "DivisionAdmin";
    };
    adminRoleSelect.addEventListener("change", updateDivisionVisibility);
    updateDivisionVisibility();
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
const attendanceActivitySelect = document.querySelector("[data-attendance-activity-select]");
if (attendanceActivitySelect instanceof HTMLSelectElement) {
    attendanceActivitySelect.addEventListener("change", () => {
        if (attendanceActivitySelect.form) {
            attendanceActivitySelect.form.requestSubmit();
        }
    });
}

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
