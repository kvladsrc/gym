#include <boost/range/algorithm/sort.hpp>
#include <cstddef>
#include <cstdint>
#include <iostream>
#include <vector>

static int64_t MaxPairwiseProduct(std::vector<int64_t>& numbers) {
  boost::range::sort(numbers);
  return numbers[numbers.size() - 1] * numbers[numbers.size() - 2];
}

int main() {
  size_t n = 0;
  std::cin >> n;
  std::vector<int64_t> numbers(n);
  for (size_t i = 0; i < n; ++i) {
    std::cin >> numbers[i];
  }

  std::cout << MaxPairwiseProduct(numbers) << "\n";
  return 0;
}
