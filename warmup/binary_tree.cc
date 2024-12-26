#include <iostream>
#include <string>

using std::string;

struct node {
  int val;
  node *left;
  node *right;

  node() : val(0), left(nullptr), right(nullptr) {}

  explicit node(int aval) : val(aval), left(nullptr), right(nullptr) {}

  ~node() {
    delete left;
    delete right;
  }

  bool search(int aval) const {
    if (val == aval) {
      return true;
    }

    if (aval > val) {
      if (right == nullptr) {
        return false;
      }

      return right->search(aval);
    }

    if (left == nullptr) {
      return false;
    }

    return left->search(aval);
  }

  void insert(int aval) {
    if (aval == val) {
      return;
    }

    if (aval > val) {
      if (right == nullptr) {
        right = new node(aval);
        return;
      }

      right->insert(aval);
      return;
    }

    if (left == nullptr) {
      left = new node(aval);
      return;
    }

    left->insert(aval);
  }

  void preorder() const {
    std::cout << val << " ";
    if (left != nullptr) {
      left->preorder();
    }
    if (right != nullptr) {
      right->preorder();
    }
  }

  void inorder() const {
    if (left != nullptr) {
      left->inorder();
    }
    std::cout << val << " ";
    if (right != nullptr) {
      right->inorder();
    }
  }

  void postorder() const {
    if (left != nullptr) {
      left->postorder();
    }
    if (right != nullptr) {
      right->postorder();
    }
    std::cout << val << " ";
  }
};

struct bin_tree {
  node *root{nullptr};
  int size{0};

  bin_tree() = default;

  ~bin_tree() { delete root; }

  void insert(int aval) {
    if (size == 0) {
      root = new node(aval);
      size++;
      return;
    }

    if (root->search(aval)) {
      return;
    }

    root->insert(aval);
    size++;
  }

  bool search(int aval) const {
    if (size == 0) {
      return false;
    }

    return root->search(aval);
  }

  void preorder() const {
    if (size == 0) {
      return;
    }
    root->preorder();
    std::cout << "\n";
  }

  void inorder() const {
    if (size == 0) {
      return;
    }
    root->inorder();
    std::cout << "\n";
  }

  void postorder() const {
    if (size == 0) {
      return;
    }
    root->postorder();
    std::cout << "\n";
  }
};

int main(int /*argc*/, char * /*argv*/[]) {
  bin_tree a;
  std::cout << a.size << "\n";

  int n = 0;
  std::cin >> n;
  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    a.insert(buf);
  }

  std::cout << a.size << "\n";
  a.preorder();
  a.inorder();
  a.postorder();

  return 0;
}
