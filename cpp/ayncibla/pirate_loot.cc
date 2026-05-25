#include <iostream>
#include <numeric>
#include <vector>

using std::vector;

static bool partition3_helper(vector<int>& A, int cur_elem, int w1, int w2,
                              int w3) {
  if (cur_elem == static_cast<int>(A.size()) && w1 == 0 && w2 == 0 && w3 == 0) {
    return true;
  }

  if (w1 < 0 || w2 < 0 || w3 < 0) {
    return false;
  }

  int const case1 = static_cast<int>(
      partition3_helper(A, cur_elem + 1, w1 - A[cur_elem], w2, w3));
  int const case2 = static_cast<int>(
      partition3_helper(A, cur_elem + 1, w1, w2 - A[cur_elem], w3));
  int const case3 = static_cast<int>(
      partition3_helper(A, cur_elem + 1, w1, w2, w3 - A[cur_elem]));

  return (case1 != 0) || (case2 != 0) || (case3 != 0);
}

static int partition3(vector<int>& A) {
  int sum = std::accumulate(A.begin(), A.end(), 0);

  if ((sum % 3) != 0) {
    return 0;
  }

  int const w = sum / 3;
  return partition3_helper(A, 0, w, w, w) ? 1 : 0;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> A(n);
  for (int& i : A) {
    std::cin >> i;
  }
  std::cout << partition3(A) << '\n';
}
