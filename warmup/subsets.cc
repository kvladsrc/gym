#include <cstddef>
#include <cstdint>
#include <iostream>
#include <vector>

using std::vector;

void print_subset(const vector<int> &a, uint64_t ss) {
  for (auto el : a) {
    if (ss % 2 != 0) {
      std::cout << el << " ";
    }
    ss /= 2;
  }
  std::cout << "\n";
}

void print_subsets(const vector<int> &a) {
  auto power = a.size();

  if (power > 64) {
    std::cout << "TOO LARGE" << "\n";
    return;
  }

  uint64_t powerset = 1;
  for (std::size_t i = 0; i < power; ++i) {
    powerset *= 2;
  }

  for (uint64_t el = 0; el < powerset; ++el) {
    print_subset(a, el);
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  print_subsets(a);

  return 0;
}
