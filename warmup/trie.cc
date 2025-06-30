#include <algorithm>
#include <cstddef>
#include <iostream>
#include <map>
#include <memory>
#include <string>

namespace {

/*
 * Very raw implementation of prefix trie. Space complexity can be
 * optimized by using a compressed suffix tree.
 */

struct node {
  std::map<char, std::unique_ptr<node>> children;
};

std::unique_ptr<node> build_trie(const std::string& s) {
  auto root = std::make_unique<node>();

  for (std::size_t idx = 0; idx < s.size(); ++idx) {
    auto* n = root.get();
    for (std::size_t cur = idx; cur < s.size(); ++cur) {
      auto& next = n->children[s[cur]];
      if (!next) {
        next = std::make_unique<node>();
      }
      n = next.get();
    }
  }

  return root;
}

bool contains(const node* root, const std::string& w) {
  for (auto ch : w) {
    auto it = root->children.find(ch);
    if (it == root->children.end()) {
      return false;
    }
    root = it->second.get();
  }

  return true;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  std::string s;
  std::cin >> s;

  std::string patt;
  std::cin >> patt;

  std::unique_ptr<node> root = build_trie(s);
  std::cout << (contains(root.get(), patt) ? "YES\n" : "NO\n");

  return 0;
}
