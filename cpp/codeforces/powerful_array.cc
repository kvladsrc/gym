#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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
#include <utility>
#include <vector>

using std::abs;
using std::endl;
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

struct mo_state {
  int l{};
  int r{};
  int64_t val{};
  vector<int64_t> coefs;

  mo_state() = default;

  mo_state(int pos, const vector<int64_t>& a) : l(pos), r(pos) {
    auto el = a[pos];
    coefs.resize(1000001, 0);
    val = el;
    coefs[el]++;
  }

  int mcost(int nl, int nr) const { return abs(l - nl) + abs(r - nr); }

  int64_t move(int nl, int nr, const vector<int64_t>& a) {
    while (r > nr) {
      auto el = a[r--];
      val -= (coefs[el] * coefs[el]) * el;
      coefs[el]--;
      val += (coefs[el] * coefs[el]) * el;
    }

    while (r < nr) {
      auto el = a[++r];
      val -= (coefs[el] * coefs[el]) * el;
      coefs[el]++;
      val += (coefs[el] * coefs[el]) * el;
    }

    while (l > nl) {
      auto el = a[--l];
      val -= (coefs[el] * coefs[el]) * el;
      coefs[el]++;
      val += (coefs[el] * coefs[el]) * el;
    }

    while (l < nl) {
      auto el = a[l++];
      val -= (coefs[el] * coefs[el]) * el;
      coefs[el]--;
      val += (coefs[el] * coefs[el]) * el;
    }

    return val;
  }
};

struct query {
  int l = 0;
  int r = 0;
  int num = 0;

  query() = default;

  query(int al, int ar, int anum) : l(--al), r(--ar), num(anum) {}
};

int main(int /*argc*/, char* /*argv*/[]) {
  size_t n = 0;
  size_t t = 0;
  std::cin >> n >> t;

  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  /*

    Mo's algorithm.

  */

  vector<query> queries(t);
  for (size_t i = 0; i < t; ++i) {
    int l = 0;
    int r = 0;
    std::cin >> l >> r;
    queries[i] = query(l, r, i);
  }

  int block_size = static_cast<int>(sqrt(n));
  sort(queries.begin(), queries.end(),
       [block_size](const auto& a, const auto& b) {
         int block_a = a.l / block_size;
         int block_b = b.l / block_size;
         if (block_a != block_b) {
           return block_a < block_b;
         }
         return a.r < b.r;
       });

  vector<int64_t> res(t);
  mo_state state(queries.front().l, a);

  for (auto q : queries) {
    res[q.num] = state.move(q.l, q.r, a);
  }

  for (auto i : res) {
    std::cout << i << "\n";
  }

  return 0;
}
