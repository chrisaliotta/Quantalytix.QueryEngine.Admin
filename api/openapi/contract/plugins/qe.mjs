// Contract rules from blueprint 01 that Redocly's configurable rules cannot express.
import { basename } from 'node:path'

const ID_NAME = /^(id|.*[a-z0-9]Id)$/
const IDS_NAME = /^.*[a-z0-9]Ids$/
const WRITE_METHODS = new Set(['post', 'put', 'patch', 'delete'])
const PROBLEM = { file: 'common.yaml', pointer: '#/components/schemas/Problem' }

const isString = (schema) => schema.type === 'string' || (Array.isArray(schema.type) && schema.type.includes('string'))
const moduleOf = (ctx) => basename(ctx.location.source.absoluteRef, '.yaml')

/** `id`, `<entity>Id` and `<entity>Ids` are strings (OI-04). */
function StringIds() {
  const check = (name, schema, ctx, location) => {
    const resolved = ctx.resolve(schema).node
    if (ID_NAME.test(name) && !isString(resolved)) {
      ctx.report({ message: `\`${name}\` is an id and must be a string (01, Ids are strings).`, location })
    }
    if (IDS_NAME.test(name) && !(resolved.type === 'array' && isString(ctx.resolve(resolved.items).node))) {
      ctx.report({ message: `\`${name}\` is a list of ids and must be an array of strings (01, Ids are strings).`, location })
    }
  }
  return {
    Schema(schema, ctx) {
      for (const [name, property] of Object.entries(schema.properties ?? {})) {
        check(name, property, ctx, ctx.location.child(['properties', name]))
      }
    },
    Parameter(parameter, ctx) {
      check(parameter.name, parameter.schema, ctx, ctx.location.child(['name']))
    },
  }
}

/** Every operation carries exactly one tag: the module whose file declares it. */
function OperationModuleTag() {
  return {
    Operation(operation, ctx) {
      const module = moduleOf(ctx)
      if (operation.tags?.length !== 1 || operation.tags[0] !== module) {
        ctx.report({ message: `Operations in ${module}.yaml are tagged exactly [${module}].`, location: ctx.location.child(['tags']) })
      }
    },
  }
}

/** Writes declare 400, and every error response is the common Problem as application/problem+json (OI-06). */
function ProblemResponses() {
  return {
    Operation: {
      enter(operation, ctx) {
        if (WRITE_METHODS.has(ctx.key) && !operation.responses['400']) {
          ctx.report({ message: 'Write operations declare a 400 Problem response.', location: ctx.location.child(['responses']) })
        }
      },
      Response(response, ctx) {
        if (Number(ctx.key) < 400) return
        const types = Object.keys(response.content ?? {})
        const schema = response.content?.['application/problem+json']?.schema
        const target = schema && ctx.resolve(schema).location
        const isProblem = target && basename(target.source.absoluteRef) === PROBLEM.file && target.pointer === PROBLEM.pointer
        if (types.length !== 1 || !isProblem) {
          ctx.report({
            message: `Error response ${ctx.key} has exactly application/problem+json with ./common.yaml${PROBLEM.pointer}.`,
            location: ctx.location.child(['content']),
          })
        }
      },
    },
  }
}

export default function qeContractPlugin() {
  return {
    id: 'qe',
    rules: {
      oas3: {
        'string-ids': StringIds,
        'operation-module-tag': OperationModuleTag,
        'problem-responses': ProblemResponses,
      },
    },
  }
}
