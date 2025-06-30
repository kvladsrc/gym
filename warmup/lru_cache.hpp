#ifndef CPP_WARMUP_LRU_CACHE_H_
#define CPP_WARMUP_LRU_CACHE_H_

#include <cstddef>
#include <list>
#include <optional>
#include <unordered_map>

namespace warmup {

class LRUCache {
  std::list<std::pair<int, int>> l;
  std::unordered_map<int, std::list<std::pair<int, int>>::iterator> m;

 public:
  LRUCache();

  std::optional<int> recent_key() const;

  bool empty() const;

  std::size_t size() const;

  std::optional<int> get(int key);

  void push(int key, int val);

 private:
  void key_used(int key);
};

}  // namespace warmup

#endif  // CPP_WARMUP_LRU_CACHE_H_
