#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/dsu.hpp"

TEST(DSUTest, OneElement) {
  warmup::DisjointSetUnion dsu(1);
  EXPECT_TRUE(dsu.connected(0, 0));
}

TEST(DSUTest, TwoElementsConnected) {
  warmup::DisjointSetUnion dsu(2);
  dsu.unite(0, 1);
  EXPECT_TRUE(dsu.connected(0, 1));
  EXPECT_TRUE(dsu.connected(1, 0));
}

TEST(DSUTest, TwoElementsDisjoined) {
  warmup::DisjointSetUnion dsu(2);
  EXPECT_FALSE(dsu.connected(0, 1));
}

TEST(DSUTest, TwoConnectedComponents) {
  warmup::DisjointSetUnion dsu(5);
  dsu.unite(0, 1);
  dsu.unite(1, 2);
  dsu.unite(3, 4);

  EXPECT_TRUE(dsu.connected(0, 2));
  EXPECT_FALSE(dsu.connected(0, 3));
  EXPECT_TRUE(dsu.connected(3, 4));
  EXPECT_FALSE(dsu.connected(2, 4));
}

TEST(DSUTest, ConnectedComponentsUnite) {
  warmup::DisjointSetUnion dsu(6);
  dsu.unite(0, 1);
  dsu.unite(2, 3);
  dsu.unite(4, 5);

  dsu.unite(1, 2);

  EXPECT_TRUE(dsu.connected(0, 3));
  EXPECT_FALSE(dsu.connected(0, 4));
  dsu.unite(3, 4);
  EXPECT_TRUE(dsu.connected(0, 5));
}

TEST(DSUTest, AlreadyJoined) {
  warmup::DisjointSetUnion dsu(3);
  dsu.unite(0, 1);
  dsu.unite(1, 2);
  EXPECT_TRUE(dsu.connected(0, 2));
  dsu.unite(0, 2);
  EXPECT_TRUE(dsu.connected(0, 2));
}
