# ExecPlan 0012 — Physics Circle-Target Narrow-Phase Kernel

> Status: **Approved — executing.** Owner approved C->B->A as 0010/0011/0012 in one batch.

## Purpose
Implement the five fixed-point circle-target predicates from Physics v13.1 §8.5.

## Design sources
- Physics v13.1 §8.5 (lines 1605-1695): PointVsUnitCircle, SweptPointVsUnitCircle, CircleVsUnitCircle, SegmentVsUnitCircle, RectVsUnitCircle, ClosestPointOnSegment. All use Dot(d,d) <= r*r (inclusive tangency).

## In scope
- ClosestPointOnSegment (degenerate zero-length -> start)
- 5 circle-target predicates (Point, SweptPoint, Circle, Segment, Rect)
- Inclusive tangency (<=)
- Negative dimension validation
- Pure EditMode tests

## Out of scope
PhysicsWorld, query DTOs, result sorting, swept Circle/Segment/Rect, hit point/distance structures.

## Results
Populated after execution.