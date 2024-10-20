﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
import { defineComponent, provide } from "vue";
import { Form, SubmissionHandler } from "vee-validate";
import RockValidation from "./rockValidation";

export type FormState = {
    submitCount: number;
};

type HandleSubmitFn = (evt: Event | SubmissionHandler, onSubmit?: SubmissionHandler) => unknown;

export default defineComponent({
    name: "RockForm",
    components: {
        Form,
        RockValidation
    },
    setup() {
        const formState = {
            submitCount: 0
        } as FormState;

        provide("formState", formState);

        return {
            formState
        };
    },
    data() {
        return {
            errorsToDisplay: []
        };
    },
    methods: {
        onInternalSubmit(handleSubmit: HandleSubmitFn, evt: Event) {
            this.formState.submitCount++;
            return handleSubmit(evt, this.emitSubmit);
        },

        emitSubmit(payload: Record<string, unknown>) {
            this.$emit("submit", payload);
        }
    },
    template: `
<Form as="" #default="{errors, handleSubmit}">
    <RockValidation :submitCount="formState.submitCount" :errors="errors" />
    <form @submit="onInternalSubmit(handleSubmit, $event)">
        <slot />
    </form>
</Form>`
});