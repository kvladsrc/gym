#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t hash(queue<int> a) {
  int res = 1;
  while (!a.empty()) {
    auto el = a.front();
    a.pop();
    res *= 10;
    res += el - 1;
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  queue<int> a;
  queue<int> b;

  int k;

  std::cin >> k;
  while (k--) {
    int buf;
    std::cin >> buf;
    a.push(buf);
  }

  std::cin >> k;
  while (k--) {
    int buf;
    std::cin >> buf;
    b.push(buf);
  }

  map<int64_t, bool> a_history;
  map<int64_t, bool> b_history;
  int fights = 0;

  while (!a.empty() && !b.empty()) {
    auto a_hash = hash(a);
    auto b_hash = hash(b);
    if (a_history[a_hash] && b_history[b_hash]) {
      std::cout << -1 << "\n";
      return 0;
    }

    fights++;
    a_history[a_hash] = true;
    b_history[b_hash] = true;

    int a_el = a.front();
    a.pop();
    int b_el = b.front();
    b.pop();

    if (a_el > b_el) {
      a.push(b_el);
      a.push(a_el);
    } else {
      b.push(a_el);
      b.push(b_el);
    }
  }

  std::cout << fights << "\n";
  if (b.empty()) {
    std::cout << 1 << "\n";
  } else {
    std::cout << 2 << "\n";
  }

  return 0;
}
