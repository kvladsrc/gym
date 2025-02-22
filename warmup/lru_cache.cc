#include <climits>
#include <cstddef>
#include <iostream>
#include <list>
#include <optional>
#include <unordered_map>

using std::list;
using std::optional;
using std::pair;
using std::unordered_map;

namespace {

/*
 * LRUCache implements a Least Recently Used (LRU) cache using a
 * combination of a doubly-linked list and an unordered_map to achieve
 * constant O(1) time complexity for all operations.
 *
 * Key Operations:
 *
 * - recent_key(): Returns the key of the most recently used element
 *                 (updated via push or get). If the cache is empty,
 *                 it returns std::nullopt.
 *
 * - get(key): Retrieves the value associated with the given key. If
 *             found, the element is moved to the front of the list
 *             to mark it as recently used.
 *
 * - push(key, val): Inserts a new key-value pair or updates an existing
 *                   key with a new value. In either case, the element
 *                   is moved to the front, marking it as the most recently
 *                   used.
 *
 * This design ensures that key lookups, updates, and insertions are
 * all performed in O(1) time.
 */

class LRUCache {
  list<pair<int, int>> l;
  unordered_map<int, list<pair<int, int>>::iterator> m;

 public:
  LRUCache() = default;

  optional<int> recent_key() const;

  bool empty() const { return l.empty(); }

  size_t size() const { return l.size(); }

  optional<int> get(int key);

  void push(int key, int val);

 private:
  void key_used(int key);
};

optional<int> LRUCache::recent_key() const {
  // EDGE_CASE: Cannot get last recently used key of empty cache.
  if (empty()) {
    return std::nullopt;
  }
  return l.front().first;
}

void LRUCache::key_used(int key) { l.splice(l.begin(), l, m[key]); }

void LRUCache::push(int key, int val) {
  auto it = m.find(key);

  if (it != m.end()) {
    auto it_l = (*it).second;
    (*it_l).second = val;
    key_used(key);
    return;
  }

  l.push_front({key, val});
  m[key] = l.begin();
}

optional<int> LRUCache::get(int key) {
  auto it = m.find(key);
  if (it == m.end()) {
    return std::nullopt;
  }

  auto it_l = (*it).second;
  auto res = (*it_l).second;
  key_used(key);
  return res;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  LRUCache c;

  for (int i = 0; i < n; ++i) {
    int key = 0;
    int val = 0;
    std::cin >> key >> val;
    c.push(key, val);
  }

  int q = 0;
  std::cin >> q;

  for (int i = 0; i < q; ++i) {
    int key = 0;
    std::cin >> key;
    auto last_used_before = c.recent_key();
    auto val = c.get(key);
    auto last_used_after = c.recent_key();

    if (last_used_before.has_value()) {
      std::cout << last_used_before.value() << " ";
    } else {
      std::cout << "MISS" << " ";
    }

    if (val.has_value()) {
      std::cout << val.value() << " ";
    } else {
      std::cout << "MISS" << " ";
    }

    if (last_used_after.has_value()) {
      std::cout << last_used_after.value() << "\n";
    } else {
      std::cout << "MISS" << "\n";
    }
  }

  return 0;
}
