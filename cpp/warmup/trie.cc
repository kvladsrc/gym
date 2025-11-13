#include "cpp/warmup/trie.hpp"

#include <map>
#include <memory>
#include <string>

namespace warmup {

Trie::Node::~Node() = default;

Trie::Trie() : root(std::make_unique<Node>()) {}

Trie::~Trie() = default;

void Trie::insert(const std::string& s) {
  for (std::size_t idx = 0; idx < s.size(); ++idx) {
    Node* n = root.get();
    for (std::size_t cur = idx; cur < s.size(); ++cur) {
      auto& next = n->children[s[cur]];
      if (!next) {
        next = std::make_unique<Node>();
      }
      n = next.get();
    }
  }
}

bool Trie::contains(const std::string& w) const {
  const Node* n = root.get();
  for (char ch : w) {
    auto it = n->children.find(ch);
    if (it == n->children.end()) {
      return false;
    }
    n = it->second.get();
  }
  return true;
}

}  // namespace warmup
