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
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void swap(int &a, int &b) {
  int buf = a;
  a = b;
  b = buf;
}

size_t partion(vector<int> &a, int start, int end) {
  int pivot = a[end];
  int less = start - 1, more = start;
  while (more < end) {
    if (a[more] < pivot) {
      swap(a[++less], a[more]);
    }

    more++;
  }
  swap(a[++less], a[end]);

  return less;
}

int nth_element(vector<int> &a, size_t n, int start, int end) {
  size_t pivot = partion(a, start, end);

  if (pivot == n) {
    return a[pivot];
  }

  if (pivot > n) {
    return nth_element(a, n, start, pivot - 1);
  }

  return nth_element(a, n, pivot + 1, end);
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << nth_element(a, (n / 2) - 1, 0, n - 1) << "\n";

  return 0;
}
