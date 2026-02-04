#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

struct tree {
  int val;
  tree *l, *r;
};

void build_tree(vector<int>& a, int l, int r, bool excl, tree* root) {
  if (l == r) {
    root->val = a[l];
    return;
  }

  int mid = (l + r) / 2;
  root->l = new tree;
  root->r = new tree;
  build_tree(a, l, mid, !excl, root->l);
  build_tree(a, mid + 1, r, !excl, root->r);

  if (excl) {
    root->val = root->l->val ^ root->r->val;
  } else {
    root->val = root->l->val | root->r->val;
  }
}

void modify_tree(tree* root, int pos, int new_val, int l, int r, bool excl) {
  if (pos < l || pos > r) {
    return;
  }

  if (l == r) {
    root->val = new_val;
    return;
  }

  int mid = (l + r) / 2;
  modify_tree(root->l, pos, new_val, l, mid, !excl);
  modify_tree(root->r, pos, new_val, mid + 1, r, !excl);

  if (excl) {
    root->val = root->l->val ^ root->r->val;
  } else {
    root->val = root->l->val | root->r->val;
  }
}

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;

  int size = 1 << n;
  bool excl = n % 2 == 0;
  vector<int> a(size);
  for (auto& i : a) {
    std::cin >> i;
  }

  auto root = new tree;
  build_tree(a, 0, size - 1, excl, root);

  while (m--) {
    int p, val;
    std::cin >> p >> val;
    modify_tree(root, p - 1, val, 0, size - 1, excl);
    std::cout << root->val << "\n";
  }

  return 0;
}
