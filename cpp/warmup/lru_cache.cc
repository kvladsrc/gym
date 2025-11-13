#include "cpp/warmup/lru_cache.hpp"

#include <optional>

namespace warmup {

/*
 * LRUCache implements a Least Recently Used (LRU) cache using a
 * combination of a doubly-linked list and an unordered_map to achieve
 * constant O(1) time complexity for all operations.
 *
 * Key Operations:
 *
 * - recent_key(): Returns the key of the most recently used element
 *                 (updated via push or get). If the cache is empty,
 * -                 it returns std::nullopt.
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

LRUCache::LRUCache() = default;

std::optional<int> LRUCache::recent_key() const {
  // EDGE_CASE: Cannot get last recently used key of empty cache.
  if (empty()) {
    return std::nullopt;
  }
  return l.front().first;
}

bool LRUCache::empty() const { return l.empty(); }

std::size_t LRUCache::size() const { return l.size(); }

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

std::optional<int> LRUCache::get(int key) {
  auto it = m.find(key);
  if (it == m.end()) {
    return std::nullopt;
  }

  auto it_l = (*it).second;
  auto res = (*it_l).second;
  key_used(key);
  return res;
}

}  // namespace warmup
