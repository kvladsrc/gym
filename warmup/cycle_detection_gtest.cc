#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/cycle_detection.hpp"

TEST(CycleDetectionTest, EmptyGraph) {
  warmup::DisjointSetUnion dsu(0);
  // No nodes, so no connections possible. This test is mostly to ensure no
  // crash.
  EXPECT_FALSE(dsu.connected(0, 0));
}

TEST(CycleDetectionTest, SingleVertexNoEdges) {
  warmup::DisjointSetUnion dsu(1);
  // A single node is always connected to itself. No cycle is formed by this.
  EXPECT_TRUE(dsu.connected(0, 0));
}

TEST(CycleDetectionTest, SelfLoop) {
  warmup::DisjointSetUnion dsu(1);
  // Attempting to unite a node with itself should indicate a cycle if already
  // connected.
  EXPECT_TRUE(dsu.connected(0, 0));
  dsu.unite(0, 0);  // This operation doesn't change state, but conceptually
                    // closes a loop
  EXPECT_TRUE(dsu.connected(0, 0));
}

TEST(CycleDetectionTest, SingleEdgeNoCycle) {
  warmup::DisjointSetUnion dsu(2);
  EXPECT_FALSE(dsu.connected(0, 1));
  dsu.unite(0, 1);
  EXPECT_TRUE(dsu.connected(0, 1));
}

TEST(CycleDetectionTest, Triangle) {
  warmup::DisjointSetUnion dsu(3);
  dsu.unite(0, 1);
  dsu.unite(1, 2);
  // Now 0 and 2 are connected. Uniting 0 and 2 should detect a cycle.
  EXPECT_TRUE(dsu.connected(0, 2));
  dsu.unite(0, 2);
  EXPECT_TRUE(dsu.connected(0, 2));
}

TEST(CycleDetectionTest, Tree) {
  warmup::DisjointSetUnion dsu(5);
  dsu.unite(0, 1);
  dsu.unite(1, 2);
  dsu.unite(2, 3);
  dsu.unite(3, 4);
  // 0 and 4 are now connected. No cycle formed yet.
  EXPECT_TRUE(dsu.connected(0, 4));
  // If we try to unite 0 and 4 again, it would indicate a cycle.
  EXPECT_TRUE(dsu.connected(0, 4));
  dsu.unite(0, 4);
  EXPECT_TRUE(dsu.connected(0, 4));
}

TEST(CycleDetectionTest, SquareWithDiagonal) {
  warmup::DisjointSetUnion dsu(4);
  dsu.unite(0, 1);
  dsu.unite(1, 2);
  dsu.unite(2, 3);
  dsu.unite(3, 0);
  // 0 and 2 are connected through 0-1-2 and 0-3-2. Uniting them should detect a
  // cycle.
  EXPECT_TRUE(dsu.connected(0, 2));
  dsu.unite(0, 2);
  EXPECT_TRUE(dsu.connected(0, 2));
}

TEST(CycleDetectionTest, DuplicateEdge) {
  warmup::DisjointSetUnion dsu(2);
  dsu.unite(0, 1);
  EXPECT_TRUE(dsu.connected(0, 1));
  // Adding the same edge again should indicate a cycle.
  EXPECT_TRUE(dsu.connected(0, 1));
  dsu.unite(0, 1);
  EXPECT_TRUE(dsu.connected(0, 1));
}

TEST(CycleDetectionTest, SeveralComponentsNoCycles) {
  warmup::DisjointSetUnion dsu(6);
  dsu.unite(0, 1);
  dsu.unite(2, 3);
  dsu.unite(4, 5);
  EXPECT_FALSE(dsu.connected(0, 2));
  EXPECT_FALSE(dsu.connected(0, 4));
  EXPECT_FALSE(dsu.connected(2, 4));
}

TEST(CycleDetectionTest, DisconnectedGraphWithOneCycle) {
  warmup::DisjointSetUnion dsu(6);
  dsu.unite(0, 1);
  dsu.unite(1, 2);
  // 0 and 2 are connected. Uniting them should detect a cycle.
  EXPECT_TRUE(dsu.connected(0, 2));
  dsu.unite(2, 0);
  EXPECT_TRUE(dsu.connected(0, 2));

  dsu.unite(3, 4);
  dsu.unite(4, 5);
  EXPECT_FALSE(dsu.connected(0, 3));
}
