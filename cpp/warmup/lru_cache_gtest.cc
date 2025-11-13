#include <gtest/gtest.h>

#include <optional>
#include <sstream>
#include <string>

#include "cpp/warmup/lru_cache.hpp"

TEST(LRUCacheTest, EmptyCache) {
  warmup::LRUCache cache;
  EXPECT_FALSE(cache.recent_key().has_value());
  EXPECT_FALSE(cache.get(5).has_value());
}

TEST(LRUCacheTest, OneElement) {
  warmup::LRUCache cache;
  cache.push(42, 100);
  EXPECT_TRUE(cache.recent_key().has_value());
  EXPECT_EQ(cache.recent_key().value(), 42);
  EXPECT_TRUE(cache.get(42).has_value());
  EXPECT_EQ(cache.get(42).value(), 100);
  EXPECT_TRUE(cache.recent_key().has_value());
  EXPECT_EQ(cache.recent_key().value(), 42);
}

TEST(LRUCacheTest, ExistingKeyNotAtFront) {
  warmup::LRUCache cache;
  cache.push(1, 10);
  cache.push(2, 20);
  cache.push(3, 30);
  EXPECT_EQ(cache.recent_key().value(), 3);
  EXPECT_EQ(cache.get(2).value(), 20);
  EXPECT_EQ(cache.recent_key().value(), 2);
}

TEST(LRUCacheTest, NonExistingKey) {
  warmup::LRUCache cache;
  cache.push(1, 10);
  cache.push(2, 20);
  cache.push(3, 30);
  EXPECT_EQ(cache.recent_key().value(), 3);
  EXPECT_FALSE(cache.get(5).has_value());
  EXPECT_EQ(cache.recent_key().value(), 3);
}

TEST(LRUCacheTest, MultipleQueries) {
  warmup::LRUCache cache;
  cache.push(1, 10);
  cache.push(2, 20);
  cache.push(3, 30);

  // Query 1
  EXPECT_EQ(cache.recent_key().value(), 3);
  EXPECT_EQ(cache.get(1).value(), 10);
  EXPECT_EQ(cache.recent_key().value(), 1);

  // Query 2
  EXPECT_EQ(cache.recent_key().value(), 1);
  EXPECT_EQ(cache.get(2).value(), 20);
  EXPECT_EQ(cache.recent_key().value(), 2);

  // Query 3
  EXPECT_EQ(cache.recent_key().value(), 2);
  EXPECT_EQ(cache.get(3).value(), 30);
  EXPECT_EQ(cache.recent_key().value(), 3);
}

TEST(LRUCacheTest, DuplicateKeys) {
  warmup::LRUCache cache;
  cache.push(1, 10);
  cache.push(1, 100);
  EXPECT_EQ(cache.recent_key().value(), 1);
  EXPECT_EQ(cache.get(1).value(), 100);
  EXPECT_EQ(cache.recent_key().value(), 1);
}

TEST(LRUCacheTest, NegativeNumbers) {
  warmup::LRUCache cache;
  cache.push(-1, -10);
  cache.push(-2, -20);
  cache.push(-3, -30);
  EXPECT_EQ(cache.recent_key().value(), -3);
  EXPECT_EQ(cache.get(-2).value(), -20);
  EXPECT_EQ(cache.recent_key().value(), -2);
}
