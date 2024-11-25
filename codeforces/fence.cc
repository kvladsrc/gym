#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int fence(vector<int> fence, int k) {
  int sum = 0, min_sum, res;
  for (int i = 0; i < k; ++i) {
    sum += fence[i];
  }
  min_sum = sum;
  res = 0;

  for (size_t i = 1; (i + k) <= fence.size(); ++i) {
    sum -= fence[i - 1];
    sum += fence[i + k - 1];

    if (sum < min_sum) {
      min_sum = sum;
      res = i;
    }
  }

  return res + 1;
}

int main(int argc, char *argv[]) {
  int n, k;
  std::cin >> n >> k;
  vector<int> f(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> f[i];
  }
  std::cout << fence(f, k) << "\n";

  return 0;
}
