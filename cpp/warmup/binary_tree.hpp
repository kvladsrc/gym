#ifndef CPP_WARMUP_BINARY_TREE_H_
#define CPP_WARMUP_BINARY_TREE_H_

#include <string>

namespace warmup {

struct node {
  int val;
  node* left;
  node* right;

  node();

  explicit node(int aval);

  ~node();

  bool search(int aval) const;

  void insert(int aval);

  void preorder() const;

  void inorder() const;

  void postorder() const;
};

struct bin_tree {
  node* root;
  int size;

  bin_tree();

  ~bin_tree();

  void insert(int aval);

  bool search(int aval) const;

  void preorder() const;

  void inorder() const;

  void postorder() const;
};

}  // namespace warmup

#endif  // CPP_WARMUP_BINARY_TREE_H_
