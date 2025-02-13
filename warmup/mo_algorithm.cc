#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <unordered_map>
#include <vector>

using std::sort;
using std::sqrt;
using std::unordered_map;
using std::vector;

struct mo_state {
  int l{};
  int r{};
  int64_t val{};
  unordered_map<int, int> cnt;

  mo_state() = default;

  mo_state(int pos, vector<int64_t>& a) : l(pos), r(pos) {
    auto el = a[pos];
    val = 1;
    cnt[el]++;
  }

  void remove(int el) {
    val -= cnt[el] * cnt[el];
    cnt[el]--;
    val += cnt[el] * cnt[el];
  }

  void add(int el) {
    val -= cnt[el] * cnt[el];
    cnt[el]++;
    val += cnt[el] * cnt[el];
  }

  int move(int nl, int nr, vector<int64_t>& a) {
    while (r > nr) {
      auto el = a[r--];
      remove(el);
    }

    while (r < nr) {
      auto el = a[++r];
      add(el);
    }

    while (l > nl) {
      auto el = a[--l];
      add(el);
    }

    while (l < nl) {
      auto el = a[l++];
      remove(el);
    }

    return val;
  }
};

struct query {
  int l;
  int r;
  int num;

  query() = default;

  query(int al, int ar, int anum) : l(al), r(ar), num(anum) {}
};

int main(int /*argc*/, char* /*argv*/[]) {
  size_t n = 0;
  size_t t = 0;
  std::cin >> n >> t;

  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  if (n == 0 || t == 0) {
    std::cout << "EMPTY" << "\n";
    return 0;
  }

  /*

    Mo's Algorithm

    1. Array Partitioning:

    The array A is divided into blocks B_1, B_2, …, B_k, where k ≈
    len(A) / sqrt(len(A)). Each block has an approximate size of
    sqrt(len(A)).

    2. Query Sorting:

    All queries are sorted using the following rules:

    - First, sort by the block number of their left index.

    - If two queries have their left indices in the same block, sort
      them by their right index. That is, for two queries q_1 and q_2,
      q_1 is considered less than q_2 if either: q_1.left is in a block
      with a smaller number, or q_1.left and q_2.left are in the same
      block and q_1.right < q_2.right.

    3. Query Processing:

    The initial state is set to left = 0 and right = 0.  For each
    query q, adjust the pointers gradually:

    - Move the left pointer one step at a time toward q.left
    (increasing or decreasing by 1) and update the answer after each
    move.

    - Similarly, adjust the right pointer one step at a time until it
    reaches q.right. This incremental adjustment allows efficient
    updating of the answer.

    4. Complexity Analysis:

    - Sorting all queries takes O(Q log Q), where Q is the number of
      queries.

    - For each block with q queries, the left pointer requires at most
    sqrt(len(A)) * q moves. The right pointer may traverse the entire
    array (worst case: from 1 to len(A) - 1), contributing up to O(N)
    moves overall, where N = len(A).

    Total: O(Q * logQ + (N+Q) * N^1/2), Q -- number of queries, N =
    len(A).

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
  mo_state state(0, a);

  for (auto q : queries) {
    res[q.num] = state.move(q.l, q.r, a);
  }

  for (auto i : res) {
    std::cout << i << "\n";
  }

  return 0;
}
