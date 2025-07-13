#ifndef CPP_WARMUP_TRIE_H_
#define CPP_WARMUP_TRIE_H_

#include <map>
#include <memory>
#include <string>

namespace warmup {

class Trie {
 public:
  Trie();
  ~Trie();
  void insert(const std::string& s);
  bool contains(const std::string& w) const;

 private:
  struct Node {
    std::map<char, std::unique_ptr<Node>> children;
    ~Node();
  };
  std::unique_ptr<Node> root;
};

}  // namespace warmup

#endif  // CPP_WARMUP_TRIE_H_
