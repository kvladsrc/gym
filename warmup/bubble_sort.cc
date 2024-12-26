#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static void swap(int &a, int &b) noexcept {
  int const buf = a;
  a = b;
  b = buf;
}

static void bubble_sort(vector<int> &a) {
  while (true) {
    bool swapped = false;

    for (size_t i = 1; i < a.size(); ++i) {
      if (a[i - 1] > a[i]) {
        swap(a[i - 1], a[i]);
        swapped = true;
      }
    }

    if (!swapped) {
      break;
    }
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  bubble_sort(a);

  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
