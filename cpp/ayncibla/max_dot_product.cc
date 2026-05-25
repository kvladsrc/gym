#include <boost/range/algorithm/sort.hpp>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <vector>

using std::vector;

static int64_t max_dot_product(vector<int> a, vector<int> b) {
  boost::range::sort(a);
  boost::range::sort(b);
  // write your code here
  int64_t result = 0;
  for (size_t i = 0; i < a.size(); i++) {
    result += (static_cast<int64_t>(a[i])) * b[i];
  }
  return result;
}

int main() {
  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  vector<int> b(n);
  for (size_t i = 0; i < n; i++) {
    std::cin >> a[i];
  }
  for (size_t i = 0; i < n; i++) {
    std::cin >> b[i];
  }
  std::cout << max_dot_product(a, b) << '\n';
}
