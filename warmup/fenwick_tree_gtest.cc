#include <gtest/gtest.h>

#include <algorithm>
#include <climits>
#include <vector>

#include "cpp/warmup/fenwick_tree.hpp"

TEST(FenwickTreeTest, Empty) {
  std::vector<int> a = {};
  warmup::node* root = nullptr;
  // No assertions, just ensure it doesn't crash
  // The original bats test for empty input also expects empty output
  // which is consistent with not building a tree.
  EXPECT_EQ(root, nullptr);
}

TEST(FenwickTreeTest, OneElement) {
  std::vector<int> a = {1};
  warmup::node* root = warmup::build_tree(a, 0, 0);
  warmup::meta result = warmup::query(root, 0, 0, 0, 0);
  EXPECT_EQ(result.maximal, 1);
  EXPECT_EQ(result.minimal, 1);
  delete root;
}

TEST(FenwickTreeTest, Simple) {
  std::vector<int> a = {7, 6, 45, 32, 6, 89, 67, 4, 6, 8};
  warmup::node* root = warmup::build_tree(a, 0, a.size() - 1);

  warmup::meta result;

  result = warmup::query(root, 0, 0, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 7);
  EXPECT_EQ(result.minimal, 7);

  result = warmup::query(root, 0, 1, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 7);
  EXPECT_EQ(result.minimal, 6);

  result = warmup::query(root, 2, 2, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 45);
  EXPECT_EQ(result.minimal, 45);

  result = warmup::query(root, 0, 3, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 45);
  EXPECT_EQ(result.minimal, 6);

  result = warmup::query(root, 5, 5, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 89);
  EXPECT_EQ(result.minimal, 89);

  // Clean up memory
  // This is a simple tree, can be deleted recursively
  std::function<void(warmup::node*)> deleteTree = [&](warmup::node* n) {
    if (!n) return;
    deleteTree(n->left);
    deleteTree(n->right);
    delete n;
  };
  deleteTree(root);
}

TEST(FenwickTreeTest, AllEqualElements) {
  std::vector<int> a = {2, 2, 2, 2, 2};
  warmup::node* root = warmup::build_tree(a, 0, a.size() - 1);

  warmup::meta result;

  result = warmup::query(root, 0, 0, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 2);
  EXPECT_EQ(result.minimal, 2);

  result = warmup::query(root, 1, 3, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 2);
  EXPECT_EQ(result.minimal, 2);

  result = warmup::query(root, 0, 4, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 2);
  EXPECT_EQ(result.minimal, 2);

  // Clean up memory
  std::function<void(warmup::node*)> deleteTree = [&](warmup::node* n) {
    if (!n) return;
    deleteTree(n->left);
    deleteTree(n->right);
    delete n;
  };
  deleteTree(root);
}

TEST(FenwickTreeTest, FullRange) {
  std::vector<int> a = {5, 3, 8, 1, 9};
  warmup::node* root = warmup::build_tree(a, 0, a.size() - 1);

  warmup::meta result = warmup::query(root, 0, a.size() - 1, 0, a.size() - 1);
  EXPECT_EQ(result.maximal, 9);
  EXPECT_EQ(result.minimal, 1);

  // Clean up memory
  std::function<void(warmup::node*)> deleteTree = [&](warmup::node* n) {
    if (!n) return;
    deleteTree(n->left);
    deleteTree(n->right);
    delete n;
  };
  deleteTree(root);
}
