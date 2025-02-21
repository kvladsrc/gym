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

  Last Recently Used cache. Key-Value data structure with the
  following methods:

  - get_last_used() -- get element with last requested key by O(1);

  - get(key) -- get element by key, move the element to the front of
    recently used elements by O(1);

  - push(key, val) -- add element to the cache, elements moved to the
    end of the list of recently used by O(1);

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
