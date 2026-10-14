import { useForm } from 'react-hook-form';
import { EntityFormSheet } from '@/shared/EntityFormSheet';
import { EntityField, JsonField, NumberField, SwitchField, TextAreaField, TextField } from '@/shared/form';
import { useCreateMember, useUpdateMember } from './api';
import { nextSortOrder, selfAndDescendants } from './memberTree';
import type { DimensionMember, MemberInput } from './types';

interface MemberValues {
  memberKey: string;
  displayName: string;
  description: string;
  parentMemberId: number | null;
  levelId: number | null;
  isAssignable: boolean;
  sortOrder: number | null;
  properties: string;
}

export interface MemberSheetProps {
  versionId: number;
  /** Every member of the version, for the parent picker and the default sort order. */
  members: DimensionMember[];
  /** The member to edit or view; null to create one under `parentId`. */
  member: DimensionMember | null;
  parentId: number | null;
  readOnly: boolean;
  onClose: () => void;
}

/** Creates, edits or shows a member: its key (fixed once created), labels, parent, level, flags, order and properties. */
export function MemberSheet({ versionId, members, member, parentId, readOnly, onClose }: MemberSheetProps) {
  const create = useCreateMember(versionId);
  const update = useUpdateMember(versionId);
  const form = useForm<MemberValues>({ defaultValues: initialValues(members, member, parentId) });
  const excluded = member ? selfAndDescendants(members, member.memberId) : new Set<number>();

  async function submit(values: MemberValues) {
    const body: MemberInput = {
      displayName: values.displayName,
      description: values.description || null,
      parentMemberId: values.parentMemberId,
      levelId: values.levelId,
      isAssignable: values.isAssignable,
      sortOrder: values.sortOrder ?? 0,
      properties: values.properties.trim() || null,
    };
    if (member) {
      await update.mutateAsync({ memberId: member.memberId, body });
      return;
    }
    await create.mutateAsync({ ...body, memberKey: values.memberKey });
  }

  return (
    <EntityFormSheet
      open
      onOpenChange={open => !open && onClose()}
      title={sheetTitle(member, readOnly)}
      form={form}
      onSubmit={submit}
      readOnly={readOnly}
      width="36rem"
    >
      <TextField
        name="memberKey"
        label="Key"
        description="The business code source values map to; fixed once created."
        disabled={member !== null}
      />
      <TextField name="displayName" label="Name" />
      <TextAreaField name="description" label="Description" />
      <EntityField
        name="parentMemberId"
        label="Parent"
        lookupKind="members"
        parentId={versionId}
        allowEmpty
        filter={item => !excluded.has(item.id)}
        description="Empty for a root member."
      />
      <EntityField name="levelId" label="Level" lookupKind="levels" parentId={versionId} allowEmpty />
      <SwitchField
        name="isAssignable"
        label="Assignable"
        description="Whether source values may map directly to this member; off for folder-only nodes."
      />
      <NumberField name="sortOrder" label="Sort order" description="Order among the member's siblings." />
      <JsonField name="properties" label="Properties" description="Optional extensible metadata as JSON." />
    </EntityFormSheet>
  );
}

function initialValues(members: DimensionMember[], member: DimensionMember | null, parentId: number | null): MemberValues {
  if (member) {
    return {
      memberKey: member.memberKey,
      displayName: member.displayName,
      description: member.description ?? '',
      parentMemberId: member.parentMemberId ?? null,
      levelId: member.levelId ?? null,
      isAssignable: member.isAssignable,
      sortOrder: member.sortOrder,
      properties: member.properties ?? '',
    };
  }
  return {
    memberKey: '',
    displayName: '',
    description: '',
    parentMemberId: parentId,
    levelId: null,
    isAssignable: true,
    sortOrder: nextSortOrder(members, parentId),
    properties: '',
  };
}

function sheetTitle(member: DimensionMember | null, readOnly: boolean) {
  if (!member) {
    return 'New member';
  }
  if (readOnly) {
    return `Member ${member.memberKey}`;
  }
  return `Edit ${member.memberKey}`;
}
