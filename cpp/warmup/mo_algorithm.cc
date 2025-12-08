#include "cpp/warmup/mo_algorithm.hpp"

#include <algorithm>
#include <cmath>
#include <vector>

using std::vector;

namespace warmup {
/*
 * Mo's Algorithm
 *
 * 1. Array Partitioning:
 *    The array A is divided into blocks B_1, B_2, â€¦, B_k, where k â‰ˆ
 *    len(A) / sqrt(len(A)). Each block has an approximate size of
 *    sqrt(len(A)).
 *
 * 2. Query Sorting:
 *    All queries are sorted using the following rules:
 *      - First, sort by the block number of their left index.
 *      - If two queries have their left indices in the same block, sort
 *        them by their right index. That is, for two queries q_1 and q_2,
 *        q_1 is considered less than q_2 if either: q_1.left is in a block
 *        with a smaller number, or q_1.left and q_2.left are in the same
 *        block and q_1.right < q_2.right.
 *
 * 3. Query Processing:
 *    The initial state is set to left = 0 and right = 0. For each query q,
 *    adjust the pointers gradually:
 *      - Move the left pointer one step at a time toward q.left
 *        (increasing or decreasing by 1) and update the answer after each
 *        move.
 *      - Similarly, adjust the right pointer one step at a time until it
 *        reaches q.right. This incremental adjustment allows efficient
 *        updating of the answer.
 *
 * 4. Complexity Analysis:
 *    - Sorting all queries takes O(Q log Q), where Q is the number of queries.
 *    - For each block with q queries, the left pointer requires at most
 *      sqrt(len(A)) * q moves. The right pointer may traverse the entire
 *      array (worst case: from 1 to len(A) - 1), contributing up to O(N)
 *      moves overall, where N = len(A).
 *
 *    Total: O(Q * logQ + (N+Q) * N^1/2), Q -- number of queries, N = len(A).
 */

mo_state::mo_state(int n) : cnt(n + 1, 0) {}

void mo_state::add(int i, const vector<int64_t>& a) {
  int64_t val = a[i];
  current_answer -= cnt[val] * cnt[val];
  cnt[val]++;
  current_answer += cnt[val] * cnt[val];
}

void mo_state::remove(int i, const vector<int64_t>& a) {
  int64_t val = a[i];
  current_answer -= cnt[val] * cnt[val];
  cnt[val]--;
  current_answer += cnt[val] * cnt[val];
}

int64_t mo_state::move(int nl, int nr, const vector<int64_t>& a) {
  while (l > nl) {
    l--;
    add(l, a);
  }
  while (r < nr) {
    r++;
    add(r, a);
  }
  while (l < nl) {
    remove(l, a);
    l++;
  }
  while (r > nr) {
    remove(r, a);
    r--;
  }
  return current_answer;
}

vector<int64_t> solve_mo(int n, const vector<int64_t>& a,
                         const vector<query>& queries) {
  int block_size = static_cast<int>(std::sqrt(n));
  vector<query> sorted_queries = queries;

  std::sort(sorted_queries.begin(), sorted_queries.end(),
            [block_size](const query& a, const query& b) {
              if (a.l / block_size != b.l / block_size) {
                return a.l / block_size < b.l / block_size;
              }
              return ((a.l / block_size) & 1) ? (a.r < b.r) : (a.r > b.r);
            });

  mo_state st(1000005);  // Assuming max value is within range
  // Actually max value in tests is small, but let's be safe or dynamic.
  // The original code used 1000005.

  vector<int64_t> answers(queries.size());
  for (const auto& q : sorted_queries) {
    answers[q.num] = st.move(q.l, q.r, a);
  }

  return answers;
}

}  // namespace warmup
